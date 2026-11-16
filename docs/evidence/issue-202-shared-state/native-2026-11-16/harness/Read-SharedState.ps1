param([Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Label)
$ErrorActionPreference='Stop'
$r=Split-Path $PSScriptRoot
$c=Get-Content "$PSScriptRoot/config.json" -Raw|ConvertFrom-Json
$f=Get-Content "$r/e2e-results/fixture.json" -Raw|ConvertFrom-Json
$login=Invoke-RestMethod -Method Post -Uri "$($c.api)/auth/login" -ContentType 'application/json' -Body (@{username=$c.admin;password=$c.password}|ConvertTo-Json)
$h=@{Authorization="Bearer $($login.accessToken)"}
$balance=Invoke-RestMethod "$($c.api)/ingredients/$($f.ingredient.ingredientId)" -Headers $h
$movementResponse=Invoke-RestMethod "$($c.api)/ingredients/$($f.ingredient.ingredientId)/movements" -Headers $h
$movements=@($movementResponse)
$receipt=Invoke-RestMethod "$($c.api)/purchase-receipts/$($f.receipt.id)" -Headers $h
$users=@{}
foreach($entry in @(@{key='windows';name=$c.winui},@{key='android';name=$c.mobile},@{key='cashier';name=$c.cashier})) {
 $s=Invoke-RestMethod -Method Post -Uri "$($c.api)/auth/login" -ContentType 'application/json' -Body (@{username=$entry.name;password=$c.password}|ConvertTo-Json)
 $users[$entry.key]=@{id=$s.user.id;username=$entry.name;roles=$s.user.roles}
 if($entry.key -eq 'cashier'){
  $response=Invoke-WebRequest "$($c.api)/ingredients/$($f.ingredient.ingredientId)" -Headers @{Authorization="Bearer $($s.accessToken)"} -SkipHttpErrorCheck
  $forbidden=@{status=[int]$response.StatusCode;protectedIngredientAbsent=!("$($response.Content)" -match 'I202 Shared Beans|availableQuantity')}
 }
}
$state=@{at=[DateTime]::UtcNow.ToString('o');balance=$balance;movements=$movements;movementCount=$movements.Count;receipt=$receipt;users=$users;cashierForbidden=$forbidden}
$state|ConvertTo-Json -Depth 12|Set-Content "$r/e2e-results/$Label-state.json"
"$Label balance=$($balance.availableQuantity) movements=$($movements.Count) receipt=$($receipt.status) cashierForbidden=$($forbidden.status)"

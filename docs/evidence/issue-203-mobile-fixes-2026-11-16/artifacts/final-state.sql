SELECT json_build_object(
 'database', current_database(),
 'serverVersion', current_setting('server_version'),
 'ingredients', (SELECT json_agg(json_build_object('id',id,'name',name,'availableQuantity',available_quantity,'reorderThreshold',reorder_threshold)) FROM inventory.ingredients WHERE name='I203 Live Beans 7146c9'),
 'movements', (SELECT json_agg(json_build_object('id',m.id,'ingredientId',m.ingredient_id,'type',m.type,'quantityDelta',m.quantity_delta,'actorId',m.actor_id,'actorUsername',u.username,'reason',m.reason,'purchaseReceiptId',m.purchase_receipt_id,'occurredAt',m.occurred_at) ORDER BY m.occurred_at) FROM inventory.stock_movements m JOIN inventory.ingredients i ON i.id=m.ingredient_id JOIN identity.users u ON u.id=m.actor_id WHERE i.name='I203 Live Beans 7146c9'),
 'receipts', (SELECT json_agg(json_build_object('id',id,'number',receipt_number,'status',status,'postedAt',posted_at)) FROM inventory.purchase_receipts WHERE receipt_number LIKE 'I203-LIVE-7146c9')
);

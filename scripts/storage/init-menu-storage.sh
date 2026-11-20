#!/bin/sh
set -eu

s3() {
    aws --endpoint-url http://storage:9000 "$@"
}

# Create only a missing bucket; authentication and connectivity failures must fail startup.
if ! s3 s3api head-bucket --bucket menu-images 2>/tmp/bucket-error; then
    if ! grep -q '(404)' /tmp/bucket-error; then
        cat /tmp/bucket-error >&2
        exit 1
    fi
    s3 s3api create-bucket --bucket menu-images >/dev/null
fi

# Anyone on loopback may read a known photo; uploads, deletion and listing require credentials.
s3 s3api put-bucket-policy --bucket menu-images --policy file:///bootstrap/menu-images-policy.json

echo 'Menu photo storage ready; existing objects preserved.'

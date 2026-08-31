#!/bin/bash
#
# fetch-asgardeo-config.sh
#
# Purpose: Pull the current self-registration, email verification, password
# policy, and verification email template config from Asgardeo, and save
# each as a JSON file under asgardeo-config/.

# NOTE: This check monitors the classic Identity Governance connector API.
# Enable/disable state for self-registration and password recovery may be
# separately controlled via Asgardeo's newer Flows system, which appears to
# use a different backend not yet reflected here.  
# Do not treat a green check here as proof the feature is enabled
# for end users — verify via live test.

set -e  # stop immediately if any command fails, instead of continuing silently

: "${CLIENT_ID:?CLIENT_ID is not set}"
: "${CLIENT_SECRET:?CLIENT_SECRET is not set}"
: "${ORG_NAME:?ORG_NAME is not set}"

BASE_URL="https://api.asgardeo.io/t/${ORG_NAME}/api/server/v1"
TOKEN_URL="https://api.asgardeo.io/t/${ORG_NAME}/oauth2/token"
OUTPUT_DIR="asgardeo-config"

echo "Requesting access token..."

TOKEN_AUTH=$(printf '%s' "${CLIENT_ID}:${CLIENT_SECRET}" | base64 -w 0)

# Note: we capture the HTTP status separately (via -w) instead of relying on
# curl's own exit code, and we don't let `set -e` kill us mid-pipeline here —
# we want to inspect the response ourselves and print a clear error if it's
# bad, rather than the script dying silently with no explanation.
set +e
TOKEN_HTTP_RESPONSE=$(curl -s -X POST "$TOKEN_URL" \
  -H "Authorization: Basic $TOKEN_AUTH" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "grant_type=client_credentials" \
  --data-urlencode "scope=internal_governance_view internal_template_mgt_view internal_email_mgt_view" \
  -w "\nHTTP_STATUS:%{http_code}")
set -e

TOKEN_STATUS=$(echo "$TOKEN_HTTP_RESPONSE" | grep -o 'HTTP_STATUS:[0-9]*' | cut -d':' -f2)
TOKEN_RESPONSE=$(echo "$TOKEN_HTTP_RESPONSE" | sed '$d')  # everything except the last line (the status marker)

if [ "$TOKEN_STATUS" != "200" ]; then
  echo "ERROR: Token request failed with HTTP $TOKEN_STATUS"
  echo "Response body: $TOKEN_RESPONSE"
  exit 1
fi

ACCESS_TOKEN=$(echo "$TOKEN_RESPONSE" | grep -o '"access_token":"[^"]*' | cut -d'"' -f4 || true)

# Remove any newline or carriage-return characters from the access token.
ACCESS_TOKEN=$(printf '%s' "$ACCESS_TOKEN" | tr -d '\r\n')

if [ -z "$ACCESS_TOKEN" ]; then
  echo "ERROR: Got HTTP 200 but couldn't find access_token in the response. Response was:"
  echo "$TOKEN_RESPONSE"
  exit 1
fi

echo "Token acquired."

mkdir -p "$OUTPUT_DIR"

fetch() {
  local description="$1"
  local url="$2"
  local outfile="$3"

  echo "Fetching: $description"
  local status
  status=$(curl -s -o "$OUTPUT_DIR/$outfile" -w "%{http_code}" \
    -X GET "$url" \
    -H "Authorization: Bearer $ACCESS_TOKEN" \
    -H "Accept: application/json")

  if [ "$status" != "200" ]; then
    echo "ERROR: $description failed with HTTP $status"
    cat "$OUTPUT_DIR/$outfile"
    exit 1
  fi
}

# Self-registration settings (enabled flag, lock-on-creation, link expiry)
fetch "Self-registration config" \
  "$BASE_URL/identity-governance/VXNlciBPbmJvYXJkaW5n/connectors/c2VsZi1zaWduLXVw" \
  "self-registration-config.json"

# Email verification settings (enabled flag, lock-on-creation, expiry)
fetch "Email verification config" \
  "$BASE_URL/identity-governance/VXNlciBPbmJvYXJkaW5n/connectors/dXNlci1lbWFpbC12ZXJpZmljYXRpb24" \
  "email-verification-config.json"

# Password history policy (reuse prevention)
fetch "Password history config" \
  "$BASE_URL/identity-governance/UGFzc3dvcmQgUG9saWNpZXM/connectors/cGFzc3dvcmRIaXN0b3J5" \
  "password-history-config.json"

# Password complexity rules (length, character requirements)
fetch "Password validation rules" \
  "$BASE_URL/validation-rules" \
  "password-validation-rules.json"

# Verification email template content (subject + HTML body)
fetch "Account confirmation email template" \
  "$BASE_URL/email/template-types/QWNjb3VudENvbmZpcm1hdGlvbg/templates/en_US" \
  "account-confirmation-template.json"

fetch "Account recovery config" \
  "$BASE_URL/identity-governance/QWNjb3VudCBNYW5hZ2VtZW50/connectors/YWNjb3VudC1yZWNvdmVyeQ" \
  "account-recovery-config.json"

fetch "Password reset OTP email template" \
  "$BASE_URL/email/template-types/UGFzc3dvcmRSZXNldE9UUA/templates/en_US" \
  "password-reset-otp-template.json"

echo "All config fetched successfully into $OUTPUT_DIR/"
#!/usr/bin/env bash
# Deploy da homologação: envia compose/.env/init.sql para o bucket privado e
# roda docker compose na EC2 via SSM Run Command. Executado pelo GitHub Actions.
set -euo pipefail

: "${AWS_REGION:?}" "${ECR_REGISTRY:?}" "${IMAGE_TAG:?}" "${DEPLOY_BUCKET:?}"
: "${AUTH_AUTHORITY:?}" "${AUTH_AUDIENCE:?}" "${COGNITO_DOMAIN:?}" "${COGNITO_CLIENT_ID:?}"
: "${QUEUE_URL:?}" "${WHATSAPP_ACCESS_TOKEN:?}"

# 1. Gera o .env do ambiente a partir dos segredos injetados pelo workflow.
cat > .env <<EOF
Authentication__Authority=${AUTH_AUTHORITY}
Authentication__Audience=${AUTH_AUDIENCE}
COGNITO_DOMAIN=${COGNITO_DOMAIN}
COGNITO_CLIENT_ID=${COGNITO_CLIENT_ID}
AWS__Region=${AWS_REGION}
AWS__QueueUrl=${QUEUE_URL}
WhatsApp__VerifyToken=${WHATSAPP_VERIFY_TOKEN:-change-me}
WhatsApp__AppSecret=${WHATSAPP_APP_SECRET:-change-me}
WhatsApp__AccessToken=${WHATSAPP_ACCESS_TOKEN}
WhatsApp__GraphVersion=${WHATSAPP_GRAPH_VERSION:-v23.0}
EOF

# 2. Envia os artefatos para o bucket privado (acessível pela instância).
aws s3 cp deploy/compose.homol.yaml "s3://${DEPLOY_BUCKET}/deploy/compose.yaml" >/dev/null
aws s3 cp infra/postgres/init.sql "s3://${DEPLOY_BUCKET}/deploy/init.sql" >/dev/null
aws s3 cp .env "s3://${DEPLOY_BUCKET}/deploy/.env" >/dev/null
rm -f .env

# 3. Localiza a instância pela tag.
INSTANCE_ID=$(aws ec2 describe-instances \
  --region "$AWS_REGION" \
  --filters "Name=tag:Name,Values=easycob-homol" "Name=instance-state-name,Values=running" \
  --query "Reservations[0].Instances[0].InstanceId" --output text)
[[ -z "$INSTANCE_ID" || "$INSTANCE_ID" == "None" ]] && { echo "Instância easycob-homol não encontrada." >&2; exit 1; }

# 4. Comando que roda na EC2 (valores do runner embutidos).
COMMANDS=$(cat <<COMMANDS
set -euo pipefail
aws s3 cp "s3://${DEPLOY_BUCKET}/deploy/compose.yaml" /opt/easycob/compose.yaml >/dev/null
aws s3 cp "s3://${DEPLOY_BUCKET}/deploy/init.sql" /opt/easycob/init.sql >/dev/null
aws s3 cp "s3://${DEPLOY_BUCKET}/deploy/.env" /opt/easycob/.env >/dev/null
cd /opt/easycob
aws ecr get-login-password --region "${AWS_REGION}" | docker login --username AWS --password-stdin "${ECR_REGISTRY}" >/dev/null
IMAGE_TAG="${IMAGE_TAG}" ECR_REGISTRY="${ECR_REGISTRY}" \
  docker compose --profile worker -f compose.yaml up -d --pull always
COMMANDS
)
B64=$(printf '%s' "$COMMANDS" | base64 -w0)

aws ssm send-command \
  --region "$AWS_REGION" \
  --instance-ids "$INSTANCE_ID" \
  --document-name "AWS-RunShellScript" \
  --comment "easycob homol deploy ${IMAGE_TAG}" \
  --timeout-seconds 900 \
  --parameters "commands=[\"echo ${B64} | base64 -d | bash\"]" \
  --wait-for-success \
  --output text --query "Command.CommandId" >/dev/null

echo "Deploy homol ${IMAGE_TAG} concluído em ${INSTANCE_ID}."

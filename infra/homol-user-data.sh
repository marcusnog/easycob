#!/bin/bash
set -euo pipefail
exec >/var/log/easycob-bootstrap.log 2>&1

dnf install -y docker git >/dev/null
systemctl enable --now docker

mkdir -p /usr/local/lib/docker/cli-plugins
curl -sSL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-x86_64 \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
chmod +x /usr/local/lib/docker/cli-plugins/docker-compose

mkdir -p /opt/easycob
usermod -aG docker ec2-user

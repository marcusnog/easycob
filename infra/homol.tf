# ===== Homologação: compose num EC2 atrás de CloudFront =====
# Decisão registrada em docs/adr/0004-deploy-homologacao.md.
# Tudo aqui é gated por environment == "homol"; outros ambientes não criam compute.

locals {
  is_homol = var.environment == "homol"
  name     = "${var.project}-${var.environment}"
  apps     = ["api", "web", "worker"]
}

data "aws_caller_identity" "current" {}

# --- ECR: imagens imutáveis por SHA ---
resource "aws_ecr_repository" "homol_app" {
  count                = local.is_homol ? length(local.apps) : 0
  name                 = "${local.name}-${local.apps[count.index]}"
  force_delete         = true
  image_tag_mutability = "MUTABLE"
}

resource "aws_ecr_lifecycle_policy" "homol_app" {
  count      = local.is_homol ? length(local.apps) : 0
  repository = aws_ecr_repository.homol_app[count.index].name
  policy = jsonencode({
    rules = [{
      rulePriority = 1
      description  = "Mantém as últimas 10 imagens"
      selection = {
        tagStatus   = "any"
        countType   = "imageCountMoreThan"
        countNumber = 10
      }
      action = { type = "expire" }
    }]
  })
}

# --- EC2: único host com Docker Compose (api, web, worker, postgres) ---
data "aws_ami" "al2023" {
  count       = local.is_homol ? 1 : 0
  most_recent = true
  owners      = ["amazon"]
  filter {
    name   = "name"
    values = ["al2023-ami-2023.*-x86_64"]
  }
  filter {
    name   = "virtualization-type"
    values = ["hvm"]
  }
}

resource "aws_iam_role" "homol_ec2" {
  count = local.is_homol ? 1 : 0
  name  = "${local.name}-ec2"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Effect = "Allow", Principal = { Service = "ec2.amazonaws.com" }, Action = "sts:AssumeRole" }]
  })
}

resource "aws_iam_role_policy_attachment" "homol_ec2_ssm" {
  count      = local.is_homol ? 1 : 0
  role       = aws_iam_role.homol_ec2[0].name
  policy_arn = "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore"
}

resource "aws_iam_role_policy_attachment" "homol_ec2_ecr" {
  count      = local.is_homol ? 1 : 0
  role       = aws_iam_role.homol_ec2[0].name
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
}

data "aws_iam_policy_document" "homol_ec2_deploy" {
  count = local.is_homol ? 1 : 0
  statement {
    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.files.arn}/deploy/*"]
  }
}

resource "aws_iam_role_policy" "homol_ec2_deploy" {
  count  = local.is_homol ? 1 : 0
  role   = aws_iam_role.homol_ec2[0].name
  policy = data.aws_iam_policy_document.homol_ec2_deploy[0].json
}

resource "aws_iam_instance_profile" "homol_ec2" {
  count = local.is_homol ? 1 : 0
  name  = "${local.name}-ec2"
  role  = aws_iam_role.homol_ec2[0].name
}

data "aws_ec2_managed_prefix_list" "cloudfront" {
  count = local.is_homol ? 1 : 0
  name  = "com.amazonaws.global.cloudfront.origin-facing"
}

resource "aws_security_group" "homol" {
  count       = local.is_homol ? 1 : 0
  name        = "${local.name}-sg"
  description = "Homologacao instance access"
  ingress {
    description = "SSH administrativo"
    from_port   = 22
    to_port     = 22
    protocol    = "tcp"
    cidr_blocks = var.ssh_cidr_allowlist
  }
  ingress {
    description  = "Web apenas via CloudFront"
    from_port    = 3001
    to_port      = 3001
    protocol     = "tcp"
    prefix_list_ids = [data.aws_ec2_managed_prefix_list.cloudfront[0].id]
  }
  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_instance" "homol" {
  count                = local.is_homol ? 1 : 0
  ami                  = data.aws_ami.al2023[0].id
  instance_type        = var.homol_instance_type
  vpc_security_group_ids = [aws_security_group.homol[0].id]
  iam_instance_profile = aws_iam_instance_profile.homol_ec2[0].name
  key_name             = var.ssh_key_name == "" ? null : var.ssh_key_name
  user_data_base64     = base64encode(file("${path.module}/homol-user-data.sh"))
  tags = {
    Name        = local.name
    Environment = var.environment
  }
  lifecycle { ignore_changes = [ami, user_data] }
}

resource "aws_eip" "homol" {
  count    = local.is_homol ? 1 : 0
  instance = aws_instance.homol[0].id
  domain   = "vpc"
}

# --- CloudFront: HTTPS com domínio padrão *.cloudfront.net (Cognito exige callback HTTPS) ---
resource "aws_cloudfront_distribution" "homol_web" {
  count    = local.is_homol ? 1 : 0
  enabled  = true
  http_version = "http2"

  origin {
    domain_name = aws_eip.homol[0].public_dns
    origin_id   = "web"
    custom_origin_config {
      http_port              = 3001
      https_port             = 443
      origin_protocol_policy = "http-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  default_cache_behavior {
    target_origin_id       = "web"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS", "PUT", "POST", "PATCH", "DELETE"]
    cached_methods         = ["GET", "HEAD"]
    forwarded_values {
      query_string = true
      cookies {
        forward = "all"
      }
      headers = ["Host", "Origin", "CloudFront-Forwarded-Proto"]
    }
    min_ttl     = 0
    default_ttl = 0
    max_ttl     = 0
    compress    = true
  }

  viewer_certificate { cloudfront_default_certificate = true }
  restrictions {
    geo_restriction { restriction_type = "none" }
  }
}

# --- Controle de custo (PLANEJAMENTO: budgets + desligamento automático de homologação) ---
resource "aws_budgets_budget" "homol" {
  count        = local.is_homol ? 1 : 0
  name         = "${local.name}-budget"
  budget_type  = "COST"
  limit_unit   = "USD"
  limit_amount = var.budget_limit_monthly
  time_unit    = "MONTHLY"
  lifecycle {
    precondition {
      condition     = var.budget_notify_email != ""
      error_message = "budget_notify_email é obrigatório para o ambiente homol."
    }
  }
  notification {
    comparison_operator        = "GREATER_THAN"
    threshold                  = 80
    threshold_type             = "PERCENTAGE"
    notification_type          = "ACTUAL"
    subscriber_email_addresses = [var.budget_notify_email]
  }
}

resource "aws_iam_role" "homol_shutdown" {
  count = local.is_homol ? 1 : 0
  name  = "${local.name}-shutdown"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Effect = "Allow", Principal = { Service = "lambda.amazonaws.com" }, Action = "sts:AssumeRole" }]
  })
}

resource "aws_iam_role_policy_attachment" "homol_shutdown_logs" {
  count      = local.is_homol ? 1 : 0
  role       = aws_iam_role.homol_shutdown[0].name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_iam_role_policy" "homol_shutdown_ec2" {
  count = local.is_homol ? 1 : 0
  role  = aws_iam_role.homol_shutdown[0].name
  policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = ["ec2:StartInstances", "ec2:StopInstances"]
      Resource = aws_instance.homol[0].arn
    }]
  })
}

data "archive_file" "homol_shutdown" {
  count       = local.is_homol ? 1 : 0
  type        = "zip"
  source_file = "${path.module}/homol-shutdown.py"
  output_path = "${path.module}/homol-shutdown.zip"
}

resource "aws_lambda_function" "homol_shutdown" {
  count            = local.is_homol ? 1 : 0
  function_name    = "${local.name}-shutdown"
  role             = aws_iam_role.homol_shutdown[0].arn
  handler          = "homol-shutdown.handler"
  runtime          = "python3.13"
  filename         = data.archive_file.homol_shutdown[0].output_path
  source_code_hash = data.archive_file.homol_shutdown[0].output_base64sha256
  environment {
    variables = { INSTANCE_ID = aws_instance.homol[0].id }
  }
}

resource "aws_cloudwatch_event_rule" "homol_stop" {
  count               = local.is_homol ? 1 : 0
  name                = "${local.name}-stop-off-hours"
  schedule_expression = "cron(0 2 * * ? *)"
}

resource "aws_cloudwatch_event_target" "homol_stop" {
  count     = local.is_homol ? 1 : 0
  rule      = aws_cloudwatch_event_rule.homol_stop[0].name
  target_id = "stop"
  arn       = aws_lambda_function.homol_shutdown[0].arn
  input     = jsonencode({ action = "stop" })
}

resource "aws_cloudwatch_event_rule" "homol_start" {
  count               = local.is_homol ? 1 : 0
  name                = "${local.name}-start-off-hours"
  schedule_expression = "cron(0 10 * * ? *)"
}

resource "aws_cloudwatch_event_target" "homol_start" {
  count     = local.is_homol ? 1 : 0
  rule      = aws_cloudwatch_event_rule.homol_start[0].name
  target_id = "start"
  arn       = aws_lambda_function.homol_shutdown[0].arn
  input     = jsonencode({ action = "start" })
}

resource "aws_lambda_permission" "homol_shutdown_stop" {
  count         = local.is_homol ? 1 : 0
  statement_id  = "AllowEventBridgeStop"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.homol_shutdown[0].function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.homol_stop[0].arn
}

resource "aws_lambda_permission" "homol_shutdown_start" {
  count         = local.is_homol ? 1 : 0
  statement_id  = "AllowEventBridgeStart"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.homol_shutdown[0].function_name
  principal     = "events.amazonaws.com"
  source_arn    = aws_cloudwatch_event_rule.homol_start[0].arn
}

# --- Outputs para a homologação ---
variable "homol_instance_type" {
  type    = string
  default = "t3.medium"
}

variable "ssh_cidr_allowlist" {
  type    = list(string)
  default = ["0.0.0.0/0"]
}

variable "ssh_key_name" {
  type    = string
  default = ""
}

variable "budget_limit_monthly" {
  type    = string
  default = "80"
}

variable "budget_notify_email" {
  type    = string
  default = ""
}

output "ecr_registry" {
  value = local.is_homol ? "${data.aws_caller_identity.current.account_id}.dkr.ecr.${var.aws_region}.amazonaws.com" : null
}

output "homol_web_url" {
  value = local.is_homol ? "https://${aws_cloudfront_distribution.homol_web[0].domain_name}" : null
}

output "homol_ec2_ip" {
  value = local.is_homol ? aws_eip.homol[0].public_ip : null
}

output "deploy_bucket" {
  value = local.is_homol ? aws_s3_bucket.files.id : null
}

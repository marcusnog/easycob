terraform {
  required_version = ">= 1.8"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 6.0"
    }
    archive = {
      source  = "hashicorp/archive"
      version = "~> 2.7"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

resource "aws_sqs_queue" "events_dlq" {
  name                      = "${var.project}-${var.environment}-events-dlq"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "events" {
  name                       = "${var.project}-${var.environment}-events"
  visibility_timeout_seconds = 60
  receive_wait_time_seconds  = 20
  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.events_dlq.arn
    maxReceiveCount     = 5
  })
}

resource "aws_s3_bucket" "files" {
  bucket = "${var.project}-${var.environment}-${var.account_suffix}"
}

resource "aws_s3_bucket_public_access_block" "files" {
  bucket                  = aws_s3_bucket.files.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "files" {
  bucket = aws_s3_bucket.files.id
  rule {
    apply_server_side_encryption_by_default { sse_algorithm = "AES256" }
  }
}

resource "aws_s3_bucket_versioning" "files" {
  bucket = aws_s3_bucket.files.id
  versioning_configuration { status = "Enabled" }
}

data "archive_file" "cognito_pre_token" {
  type        = "zip"
  source_file = "${path.module}/cognito-pre-token.mjs"
  output_path = "${path.module}/cognito-pre-token.zip"
}

resource "aws_iam_role" "cognito_pre_token" {
  name = "${var.project}-${var.environment}-cognito-pre-token"
  assume_role_policy = jsonencode({
    Version   = "2012-10-17"
    Statement = [{ Effect = "Allow", Principal = { Service = "lambda.amazonaws.com" }, Action = "sts:AssumeRole" }]
  })
}

resource "aws_iam_role_policy_attachment" "cognito_pre_token_logs" {
  role       = aws_iam_role.cognito_pre_token.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

resource "aws_lambda_function" "cognito_pre_token" {
  function_name    = "${var.project}-${var.environment}-cognito-pre-token"
  role             = aws_iam_role.cognito_pre_token.arn
  handler          = "cognito-pre-token.handler"
  runtime          = "nodejs22.x"
  filename         = data.archive_file.cognito_pre_token.output_path
  source_code_hash = data.archive_file.cognito_pre_token.output_base64sha256
}

resource "aws_cognito_user_pool" "users" {
  name                     = "${var.project}-${var.environment}"
  username_attributes      = ["email"]
  auto_verified_attributes = ["email"]
  user_pool_tier           = "ESSENTIALS"

  password_policy {
    minimum_length                   = 12
    require_lowercase                = true
    require_numbers                  = true
    require_symbols                  = true
    require_uppercase                = true
    temporary_password_validity_days = 7
  }

  schema {
    name                = "tenant_id"
    attribute_data_type = "String"
    mutable             = true
    required            = false
    string_attribute_constraints {
      min_length = 36
      max_length = 36
    }
  }

  lambda_config {
    pre_token_generation_config {
      lambda_arn     = aws_lambda_function.cognito_pre_token.arn
      lambda_version = "V2_0"
    }
  }
}

resource "aws_lambda_permission" "cognito_pre_token" {
  statement_id  = "AllowCognitoInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.cognito_pre_token.function_name
  principal     = "cognito-idp.amazonaws.com"
  source_arn    = aws_cognito_user_pool.users.arn
}

resource "aws_cognito_user_pool_domain" "users" {
  domain       = var.cognito_domain_prefix
  user_pool_id = aws_cognito_user_pool.users.id
}

resource "aws_cognito_user_pool_client" "web" {
  name                                 = "${var.project}-${var.environment}-web"
  user_pool_id                         = aws_cognito_user_pool.users.id
  generate_secret                      = false
  allowed_oauth_flows_user_pool_client = true
  allowed_oauth_flows                  = ["code"]
  allowed_oauth_scopes                 = ["openid", "email", "profile"]
  callback_urls                        = var.cognito_callback_urls
  logout_urls                          = var.cognito_logout_urls
  supported_identity_providers         = ["COGNITO"]
  access_token_validity                = 60
  id_token_validity                    = 60
  refresh_token_validity               = 30
  prevent_user_existence_errors        = "ENABLED"

  token_validity_units {
    access_token  = "minutes"
    id_token      = "minutes"
    refresh_token = "days"
  }
}

resource "aws_cognito_user_group" "roles" {
  for_each     = toset(["Owner", "Admin", "Finance", "Collector", "Viewer"])
  name         = each.value
  user_pool_id = aws_cognito_user_pool.users.id
}

variable "aws_region" {
  type    = string
  default = "sa-east-1"
}

variable "project" {
  type    = string
  default = "easycob"
}

variable "environment" {
  type    = string
  default = "dev"
}

variable "account_suffix" {
  type        = string
  description = "Sufixo globalmente único para o bucket."
}

variable "cognito_domain_prefix" {
  type        = string
  description = "Prefixo globalmente único do domínio Cognito."
}

variable "cognito_callback_urls" {
  type    = list(string)
  default = ["http://localhost:3001/api/auth/callback"]
}

variable "cognito_logout_urls" {
  type    = list(string)
  default = ["http://localhost:3001/login"]
}

output "queue_url" { value = aws_sqs_queue.events.url }
output "dlq_url" { value = aws_sqs_queue.events_dlq.url }
output "bucket_name" { value = aws_s3_bucket.files.id }
output "cognito_user_pool_id" { value = aws_cognito_user_pool.users.id }
output "cognito_client_id" { value = aws_cognito_user_pool_client.web.id }
output "cognito_domain" { value = "https://${aws_cognito_user_pool_domain.users.domain}.auth.${var.aws_region}.amazoncognito.com" }
output "cognito_authority" { value = "https://cognito-idp.${var.aws_region}.amazonaws.com/${aws_cognito_user_pool.users.id}" }

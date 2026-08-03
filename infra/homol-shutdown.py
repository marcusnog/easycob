import os

import boto3


def handler(event, _context):
    action = event.get("action", "stop")
    instance_id = os.environ["INSTANCE_ID"]
    ec2 = boto3.client("ec2", region_name=os.environ.get("AWS_REGION", "sa-east-1"))
    if action == "stop":
        ec2.stop_instances(InstanceIds=[instance_id])
    else:
        ec2.start_instances(InstanceIds=[instance_id])

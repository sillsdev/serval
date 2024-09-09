# %%
import boto3

bucket_name = "silnlp"
prefix = ""
total_sizes = {}

counter = 0
for obj in boto3.resource("s3").Bucket(bucket_name).objects.filter(Prefix=prefix):
    key = obj.key
    prefix = key.split("/")[0]
    if prefix not in total_sizes:
        total_sizes[prefix] = 0
    total_sizes[prefix] += obj.size
    counter += 1
    if counter % 10_000 == 0:
        for prefix, size in total_sizes.items():
            print(f"{prefix}: {size/1e6:.2f} MB")
    if counter > 1_000_000:
        break
for prefix, size in total_sizes.items():
    print(f"{prefix}: {size/1e6:.2f} MB")

# %%

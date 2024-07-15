from serval_client_module import RemoteCaller
from serval_auth_module import ServalBearerAuth
import argparse
import os
from pathlib import Path
from tqdm import tqdm


def main():
    parser = argparse.ArgumentParser(description="Pull all USFM for testing")
    parser.add_argument(
        "--client-id",
        default="",
        help="Serval client id (if none is provided env var SERVAL_CLIENT_ID will be used)",
    )
    parser.add_argument(
        "--client-secret",
        default="",
        help="Serval client secret (if none is provided env var SERVAL_CLIENT_SECRET will be used)",
    )
    parser.add_argument(
        "--output-dir", default="usfm", help="Output directory for usfm files"
    )
    args = parser.parse_args()
    serval_auth = ServalBearerAuth(
        client_id=args.client_id, client_secret=args.client_secret
    )
    client = RemoteCaller(
        url_prefix=os.environ.get("SERVAL_HOST_URL"), auth=serval_auth
    )

    output_dir = Path(args.output_dir)
    if not output_dir.exists():
        output_dir.mkdir()

    all_files = client.data_files_get_all()
    for file in tqdm(list(filter(lambda f: f.format == "Paratext", all_files))):
        try:
            file_data = client.data_files_download(file.id)
            with open(output_dir / file.name, "wb") as f:
                f.write(file_data.read())
        except Exception as e:
            print(f"Failed to download file {file.name} because of exception {e}")


if __name__ == "__main__":
    main()

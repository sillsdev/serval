#! /usr/bin/python3
import json
import os, stat, time
import urllib3
import shutil
import random
import string
from tqdm import tqdm

def main():
    start = time.time()
    SERVAL_AUTH_URL = os.environ.get("SERVAL_AUTH_URL")
    SERVAL_CLIENT_ID = os.environ.get("SERVAL_CLIENT_ID")
    SERVAL_CLIENT_SECRET = os.environ.get("SERVAL_CLIENT_SECRET")
    REQUESTS_PER_SECOND = 5
    NUM_CONCURRENT_CONNECTIONS = 20
    NUM_NMT_ENGINES_TO_ADD = 10_000
    NUM_SMT_ENGINES_TO_ADD = 500

    base_url = "localhost" #"https://qa-int.serval-api.org"

    urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

    print('Fetching authorization token...')
    data = {
        "client_id": f"{SERVAL_CLIENT_ID}",
        "client_secret":f"{SERVAL_CLIENT_SECRET}",
        "audience":"https://serval-api.org",
        "grant_type":"client_credentials"
        }

    encoded_data = json.dumps(data).encode('utf-8')

    http = urllib3.PoolManager() #Use the following parameters if base_url is not localhost: cert_reqs='CERT_NONE', assert_hostname=False
    r:urllib3.response.HTTPResponse =http.request(
        'POST',
        f'{SERVAL_AUTH_URL}/oauth/token',
        body=encoded_data,
        headers={"content-type": "application/json"}
        )
    access_token = json.loads(r.data.decode('utf-8'))['access_token']

    src_id = ""
    trg_id = ""

    print('Setting up bombardier...')
    bombardier_path_to_exe = 'load_testing_data/bombardier-linux-amd64'
    if not os.path.exists(bombardier_path_to_exe):
        with http.request('GET', 'https://github.com/codesenberg/bombardier/releases/download/v1.2.6/bombardier-linux-amd64', preload_content=False) as r, open(bombardier_path_to_exe, 'wb') as out_file:
            shutil.copyfileobj(r, out_file)

    os.chmod(bombardier_path_to_exe, mode=stat.S_IXUSR)
    try:
        print('Bombarding get all translation engines endpoint...')
        os.system(
            "./"
            + bombardier_path_to_exe
            + f' --print r -k -l -d 60s -r {REQUESTS_PER_SECOND} -c {NUM_CONCURRENT_CONNECTIONS} -H "authorization: Bearer {access_token}" -H "accept: application/json" -m "GET"  "{base_url}/api/v1/translation/engines"')

        print("Posting engines to DB...")
        nmt_engine_ids:set[str] = set()
        smt_engine_ids:set[str] = set()
        def post_nmt_engine():
            r:urllib3.response.HTTPResponse =http.request(
                'POST',
                f'{base_url}/api/v1/translation/engines',
                body = json.dumps({"name": "load_testing_engine", "targetLanguage": "en_Latn", "sourceLanguage" : "ell_Grek", "type":"Nmt"}).encode('utf-8'),
                headers={"content-type": "application/json", "accept":"application/json", "authorization" : f"Bearer {access_token}"}
            )
            nmt_engine_ids.add(json.loads(r.data.decode('utf-8'))['id'])

        print("Posting NMT")
        for _ in tqdm(range(NUM_NMT_ENGINES_TO_ADD)):
            post_nmt_engine()

        def post_smt_engine():
            r:urllib3.response.HTTPResponse =http.request(
                'POST',
                f'{base_url}/api/v1/translation/engines',
                body = json.dumps({"name": "load_testing_engine", "targetLanguage": "en_Latn", "sourceLanguage" : "ell_Grek", "type":"SmtTransfer"}).encode('utf-8'),
                headers={"content-type": "application/json", "accept":"application/json", "authorization" : f"Bearer {access_token}"}
            )
            smt_engine_ids.add(json.loads(r.data.decode('utf-8'))['id'])

        print("Posting SMT")
        for _ in tqdm(range(NUM_SMT_ENGINES_TO_ADD)):
            post_smt_engine()

        #use bombadier get
        print('Bombarding get all translation engines endpoint after adding docs...')
        os.system(
            "./"
            + bombardier_path_to_exe
            + f' --print r -k -l -d 60s -r {REQUESTS_PER_SECOND} -c {NUM_CONCURRENT_CONNECTIONS} -H "authorization: Bearer {access_token}" -H "accept: application/json" -m "GET"  "{base_url}/api/v1/translation/engines"')
        #add necessary files
        print('Adding corpus to smt engine...')
        with open('load_testing_data/testsrc.txt', 'r') as src_file:
            src_data = src_file.read()
            r:urllib3.response.HTTPResponse =http.request_encode_body(
                'POST',
                f'{base_url}/api/v1/files',
                fields={
                    'file': ('testsrc.txt', src_data, 'text/plain'),
                    'format':'Text'
                    },
                headers={'accept' : 'application/json', "authorization" : f"Bearer {access_token}"},
                encode_multipart=True
            )
            src_id = json.loads(r.data.decode('utf-8'))['id']

        with open('load_testing_data/testtarg.txt', 'r') as targ_file:
            targ_data = targ_file.read()
            r:urllib3.response.HTTPResponse =http.request(
                'POST',
                f'{base_url}/api/v1/files',
                fields={'file':('testtarg.txt', targ_data, 'text/plain'), 'format':'Text'},
                headers={'accept': 'application/json', "authorization" : f"Bearer {access_token}"}
            )
            trg_id = json.loads(r.data.decode('utf-8'))['id']

        print("Building an SMT engine for bombardment...")
        #add corpora and build an smt
        smt_id = list(smt_engine_ids)[0]
        http.request(
            'POST',
            f'{base_url}/api/v1/translation/engines/{smt_id}/corpora',
            body=json.dumps(
                {
                    "sourceLanguage":"ell_Grek",
                    "targetLanguage":"en_Latn",
                    "sourceFiles":
                        [
                            {
                                "fileId" : src_id,
                                "textId":"all"
                            }
                        ],
                    "targetFiles":
                        [
                            {
                                "fileId" : trg_id,
                                "textId":"all"
                            }
                        ]
                }).encode('utf-8'),
            headers={'content-type':'application/json', 'accept': 'application/json', "authorization" : f"Bearer {access_token}"}
        )
        # corpus_id = json.loads(r.data.decode('utf-8'))['id']
        r:urllib3.response.HTTPResponse = http.request(
            'POST',
            f'{base_url}/api/v1/translation/engines/{smt_id}/builds',
            body=json.dumps({}).encode('utf-8'),
            headers={"authorization" : f"Bearer {access_token}", 'content-type':'application/json'}
        )

        #waiting for build
        is_built = False
        retry_index = 0
        while not is_built:
            r:urllib3.response.HTTPResponse = http.request(
                'GET',
                f'{base_url}/api/v1/translation/engines/{smt_id}/current-build',
                headers={"authorization" : f"Bearer {access_token}"}
            )
            is_built = r.status == 204
            if r.status//100 != 2:
                raise Exception(f"Received response of {r.status} while trying to build engine; cannot continue testing!")
            if retry_index > 15:
                r:urllib3.response.HTTPResponse = http.request(
                    'POST',
                    f'{base_url}/api/v1/translation/engines/{smt_id}/current-build/cancel',
                    headers={"authorization" : f"Bearer {access_token}"}
                )

                print("Engine is taking too long to build to continue testing. Cancelling build...")
                raise Exception("Engine is taking too long to build to continue testing. Cancelling build...")
            time.sleep(60 if retry_index == 0 else 20*retry_index)
            retry_index += 1

        segment_file_name = "".join(random.choices(string.ascii_letters, k=24)) + '.json'
        f = open(segment_file_name, 'w', encoding='utf-8')
        f.write(json.dumps("Βίβλος γενέσεως Ἰησοῦ Χριστοῦ"))
        f.flush()
        f.close()

        #bombard get word graph
        print("Bombarding word graph endpoint...")
        os.system(
            "./"
            + bombardier_path_to_exe
            + f' --print r -k -l -d 60s -r {REQUESTS_PER_SECOND} -c {NUM_CONCURRENT_CONNECTIONS} -H "authorization: Bearer {access_token}" -H "accept: application/json" -H "content-type: application/json" -m "POST"  "{base_url}/api/v1/translation/engines/{smt_id}/get-word-graph" '
            + f'-f "{segment_file_name}"'
        )

        os.remove(segment_file_name)

        nmt_id = list(nmt_engine_ids)[0]

        print("Building NMT engine...")
        r:urllib3.response.HTTPResponse = http.request(
            'POST',
            f'{base_url}/api/v1/translation/engines/{nmt_id}/corpora',
            body=json.dumps(
                {
                    "sourceLanguage":"ell_Grek",
                    "targetLanguage":"en_Latn",
                    "sourceFiles":
                        [
                            {
                                "fileId" : src_id,
                                "textId":"all"
                            }
                        ],
                    "targetFiles":
                        [
                            {
                                "fileId" : trg_id,
                                "textId":"all"
                            }
                        ]
                }).encode('utf-8'),
            headers={'content-type':'application/json', 'accept': 'application/json', "authorization" : f"Bearer {access_token}"}
        )

        corpus_id = json.loads(r.data.decode('utf-8'))['id']

        r:urllib3.response.HTTPResponse = http.request(
            'POST',
            f'{base_url}/api/v1/translation/engines/{nmt_id}/builds',
            body=json.dumps(
                {
                    "pretranslate" : [
                        {
                            "corpusId": corpus_id,
                            "textIds": [
                                "all"
                            ]
                        }
                    ],
                    "options":"{\"max_steps\":10}"
                }
            ),
            headers={"authorization" : f"Bearer {access_token}", 'content-type':'application/json'}
        )

        #waiting for build
        is_built = False
        retry_index = 0
        while not is_built:
            r:urllib3.response.HTTPResponse = http.request(
                'GET',
                f'{base_url}/api/v1/translation/engines/{nmt_id}/current-build',
                headers={"authorization" : f"Bearer {access_token}"}
            )
            is_built = r.status == 204
            if r.status//100 != 2:
                raise Exception(f"Received response of {r.status} while trying to build engine; cannot continue testing!")
            if retry_index > 15:
                print("Engine is taking too long to build to continue testing. Cancelling build...")
                r:urllib3.response.HTTPResponse = http.request(
                    'POST',
                    f'{base_url}/api/v1/translation/engines/{nmt_id}/current-build/cancel',
                    headers={"authorization" : f"Bearer {access_token}"}
                )
                raise Exception("Engine is taking too long to build to continue testing. Cancelling build...")
            time.sleep(240 if retry_index == 0 else 60*retry_index)
            retry_index += 1

        print("Bombarding pretranslation endpoint...")
        #bombard get pretrans
        os.system(
            "./"
            + bombardier_path_to_exe
            + f' --print r -k -l -d 60s -r {REQUESTS_PER_SECOND} -c {NUM_CONCURRENT_CONNECTIONS} -H "authorization: Bearer {access_token}" -H "accept: application/json" -m "GET"  "{base_url}/api/v1/translation/engines/{nmt_id}/corpora/{corpus_id}/pretranslations" '
        )
    except Exception as e:
        print("Something went wrong:", str(e) if str(e) != "" else "[No information]")
    finally:
        #cleanup files, smt, nmt, bombardier
        print('Cleaning up...')
        print('Deleting added translation engines...')
        def delete_engine(engine_id):
            r:urllib3.response.HTTPResponse =http.request(
                'DELETE',
                f'{base_url}/api/v1/translation/engines/{engine_id}',
                body = json.dumps({"id":engine_id}).encode('utf-8'),
                headers={"content-type": "application/json", "authorization" : f"Bearer {access_token}"}
            )
            if(r.status != 200):
                print(f"Failed to delete engine {engine_id}")

        for engine_id in tqdm(nmt_engine_ids):
            delete_engine(engine_id)
        for engine_id in tqdm(smt_engine_ids):
            delete_engine(engine_id)

        r:urllib3.response.HTTPResponse =http.request(
            'DELETE',
            f'{base_url}/api/v1/files/{src_id}',
            headers={"content-type": "application/json", "authorization" : f"Bearer {access_token}"}
        )
        if(r.status != 200):
            print(f"Failed to delete file {src_id}")

        r:urllib3.response.HTTPResponse =http.request(
            'DELETE',
            f'{base_url}/api/v1/files/{trg_id}',
            headers={"content-type": "application/json", "authorization" : f"Bearer {access_token}"}
        )
        if(r.status != 200):
            print(f"Failed to delete file {trg_id}")


        os.remove(bombardier_path_to_exe)
        print("Finished testing in", round((time.time()-start)/60, 2), "minutes.")

if __name__ == "__main__":
    main()
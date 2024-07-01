import os

import bson
import pymongo
import pymongo.write_concern

# Set write concern to 0 to avoid waiting for the write to be acknowledged
pymongo.write_concern.WriteConcern(w=0)

MONGO_CONNECTION_STRING = os.environ["MONGO_CONNECTION_STRING"]
MONGO_PREFIX = "qa_int_"
client = pymongo.MongoClient(MONGO_CONNECTION_STRING)


def get_full_collection(database_name: str, collection_name: str):
    db = client[database_name]
    db_connection = db[collection_name]
    items = []
    for batch in db_connection.find_raw_batches():
        items.extend(bson.decode_all(batch))
    return items


def update_full_collection(database_name: str, collection_name: str, items: list):
    db = client[database_name]
    db_connection = db[collection_name]
    for item in items:
        db_connection.update_one({"_id": item["_id"]}, {"$set": {"type": item["type"]}})


machine_engines: dict = {
    e["engineId"]: e
    for e in get_full_collection(MONGO_PREFIX + "machine", "translation_engines")
}
serval_engines: dict = {
    str(e["_id"]): e
    for e in get_full_collection(MONGO_PREFIX + "serval", "translation.engines")
}

for key in machine_engines.keys():
    if key in serval_engines:
        if "type" in machine_engines[key].keys():
            print(f"Engine {key} is already type {machine_engines[key]['type']}")
        else:
            machine_engines[key]["type"] = serval_engines[key]["type"]
            print(f"Engine {key} to be updated with type {serval_engines[key]['type']}")
    else:
        print(f"Engine {key} not found in serval database")

# update machine engines
update_full_collection(
    MONGO_PREFIX + "machine", "translation_engines", list(machine_engines.values())
)

# %%
import os

import bson
import pymongo
import pymongo.write_concern

# Set write concern to 0 to avoid waiting for the write to be acknowledged
pymongo.write_concern.WriteConcern(w=0)

MONGO_CONNECTION_STRING = os.environ["MONGO_CONNECTION_STRING"]
MONGO_PREFIX = "prod_"
client = pymongo.MongoClient(MONGO_CONNECTION_STRING)


# %%
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


# %%
machine_engines: dict = {
    e["engineId"]: e
    for e in get_full_collection(MONGO_PREFIX + "machine", "translation_engines")
}
serval_engines: dict = {
    str(e["_id"]): e
    for e in get_full_collection(MONGO_PREFIX + "serval", "translation.engines")
}


def update_engine_type(machine_engines: dict, serval_engines: dict):

    for key in machine_engines.keys():
        if key in serval_engines:
            if "type" in machine_engines[key].keys():
                print(f"Engine {key} is already type {machine_engines[key]['type']}")
            else:
                machine_engines[key]["type"] = serval_engines[key]["type"]
                print(
                    f"Engine {key} to be updated with type {serval_engines[key]['type']}"
                )
        else:
            print(f"Engine {key} not found in serval database")

    # update machine engines
    update_full_collection(
        MONGO_PREFIX + "machine", "translation_engines", list(machine_engines.values())
    )


# update_engine_type(machine_engines, serval_engines)


# %% update jobRunner to buildJobRunner in currentBuild
def update_current_build(database_name: str, collection_name: str, items: list):
    db = client[database_name]
    db_connection = db[collection_name]
    for item in items:
        db_connection.update_one(
            {"_id": item["_id"]},
            {"$set": {"currentBuild": item["currentBuild"]}},
        )
        print(f"Updated {item['_id']}")


engines_to_update = []
for machine_engine_key in machine_engines.keys():
    machine_engine = machine_engines[machine_engine_key]
    if "currentBuild" in machine_engine:
        currentBuild = machine_engine["currentBuild"]
        if "jobRunner" in currentBuild:
            currentBuild["buildJobRunner"] = currentBuild.pop("jobRunner")
            engines_to_update.append(machine_engine)
            print(machine_engine["currentBuild"])
        else:
            print(f"Already updated {machine_engine_key}")
    else:
        print(f"No currentBuild for {machine_engine_key}")
# %%
update_current_build(MONGO_PREFIX + "machine", "translation_engines", engines_to_update)

# %%

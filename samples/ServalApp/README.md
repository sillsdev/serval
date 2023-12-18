### Running the Serval APP
Before running the app, verify that `SERVAL_APP_EMAIL_PASSWORD` is populated appropriately.
Then, run:
```
streamlit run serval_app.py
```

### Regenerating the Python Client
When the Serval API is updated, download the "swagger.json" from the swagger endpoint and use the tool [swagger-to](https://pypi.org/project/swagger-to/) to generate a new `serval_client_module.py` using the following command in this directory:
```
swagger_to_py_client.py --swagger_path path/to/swagger.json --outpath serval_client_module.py
```
Note: You may need to delete the authorization-related elements of the "swagger.json" before generating.
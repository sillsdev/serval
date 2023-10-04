python3 send_updates.py &
SEND_UPDATES_PID=$!
streamlit run serval_app.py
kill $SEND_UPDATES_PID
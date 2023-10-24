import smtplib
import ssl
from email.message import EmailMessage


class ServalAppEmailServer:
    def __init__(
        self,
        password,
        sender_address="serval-app@languagetechnology.org",
        host="mail.languagetechnology.org",
        port=465,
    ) -> None:
        self.__password = password
        self.sender_address = sender_address
        self.host = host
        self.port = port
        self.server = None

    @property
    def password(self):
        return len(self.__password) * "*"

    def __enter__(self):
        context = ssl.create_default_context()
        self.server = smtplib.SMTP_SSL(host=self.host, port=self.port, context=context)
        self.server.login(self.sender_address, self.__password)
        return self

    def __exit__(self, *args):
        self.server.close()

    def send_build_completed_email(
        self, recipient_address: str, pretranslations_file_data: str, build_info: str
    ):
        msg = EmailMessage()
        msg.set_content(
            f"""Hi!

Your NMT engine has completed building. Attached are the \
    translations of untranslated source text in the files you included.

If you are experiencing difficulties using this application, please contact eli_lowry@sil.org.

Thank you!

{build_info}
"""
        )
        msg["From"] = self.sender_address
        msg["To"] = recipient_address
        msg["Subject"] = "Your NMT build job is complete!"
        msg.add_attachment(pretranslations_file_data, filename="translations.txt")
        self.server.send_message(msg)

    def send_build_faulted_email(
        self, recipient_address: str, build_info: str, error=""
    ):
        msg = EmailMessage()
        msg.set_content(
            f"""Hi!

Your NMT engine has failed to build{" with the following error message: " + error if error != "" else ""}. \
    Please make sure the information you specified is correct and try again after a while.

If you continue to experience difficulties using this application, please contact eli_lowry@sil.org.

Thank you!

{build_info}
"""
        )
        msg["From"] = self.sender_address
        msg["To"] = recipient_address
        msg["Subject"] = "Your NMT build job has failed"
        self.server.send_message(msg)

    def send_build_started_email(self, recipient_address: str, build_info: str):
        msg = EmailMessage()
        msg.set_content(
            f"""Hi!

Your NMT engine has started building. We will contact you when it is complete.

If you are experiencing difficulties using this application, please contact eli_lowry@sil.org.

Thank you!
{build_info}
"""
        )
        msg["From"] = self.sender_address
        msg["To"] = recipient_address
        msg["Subject"] = "Your NMT build job has started building!"

        self.server.send_message(msg)

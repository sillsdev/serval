import smtplib
import ssl
from email.mime.application import MIMEApplication
from email.mime.multipart import MIMEMultipart
from email.mime.text import MIMEText
from email.utils import formatdate

from db import Build


class ServalAppEmailServer:
    def __init__(
        self,
        password,
        sender_address="serval-app@languagetechnology.org",
        host="gator4145.hostgator.com",  # "mail.languagetechnology.org",
        port=587,
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
        # self.context = ssl.create_default_context()
        self.server = smtplib.SMTP(host=self.host, port=self.port)
        self.server.set_debuglevel(1)
        self.server.starttls()
        self.server.login(self.sender_address, self.__password)
        return self

    def __exit__(self, *args):
        self.server.quit()
        self.server.close()

    def send_build_completed_email(
        self, recipient_address: str, pretranslations_file_data: str, build: Build
    ):
        build_info = str(build)
        msg = MIMEMultipart()
        msg.attach(
            MIMEText(
                f"""Hi!

Your NMT engine has completed building. Attached are the \
    translations of untranslated source text in the files you included.

If you are experiencing difficulties using this application, please contact eli_lowry@sil.org.

Thank you!

{build_info}
"""
            )
        )
        msg["From"] = self.sender_address
        msg["To"] = recipient_address
        msg["Date"] = formatdate(localtime=True)
        msg["Subject"] = "Your NMT build job is complete!"
        part = MIMEApplication(pretranslations_file_data, Name=f"{build.name}.txt")
        part["Content-Disposition"] = f'attachment; filename="{build.name}.txt"'
        msg.attach(part)
        errs = self.server.send_message(msg)
        print(errs)

    def send_build_faulted_email(
        self, recipient_address: str, build_info: str, error=""
    ):
        msg = MIMEMultipart()
        msg.attach(
            MIMEText(
                f"""Hi!

Your NMT engine has failed to build{" with the following error message: " + error if error != "" else ""}. \
    Please make sure the information you specified is correct and try again after a while.

If you continue to experience difficulties using this application, please contact eli_lowry@sil.org.

Thank you!

{build_info}
"""
            )
        )
        msg["From"] = self.sender_address
        msg["To"] = recipient_address
        msg["Date"] = formatdate(localtime=True)
        msg["Subject"] = "Your NMT build job has failed"
        errs = self.server.send_message(msg)
        print(errs)

    def send_build_started_email(self, recipient_address: str, build_info: str):
        msg = MIMEText(
            f"""Hi!

Your NMT engine has started building. We will contact you when it is complete.

If you are experiencing difficulties using this application, please contact eli_lowry@sil.org.

Thank you!
{build_info}
"""
        )
        msg["From"] = self.sender_address
        msg["To"] = recipient_address
        msg["Date"] = formatdate(localtime=True)
        msg["Subject"] = "Your NMT build job has started building!"
        errs = self.server.send_message(msg)
        print(errs)

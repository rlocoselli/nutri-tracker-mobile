import smtplib
from email.message import EmailMessage
from .config import (
    SMTP_HOST,
    SMTP_PORT,
    SMTP_USERNAME,
    SMTP_PASSWORD,
    SMTP_FROM_EMAIL,
    SMTP_USE_TLS,
)


def smtp_configured() -> bool:
    return bool(SMTP_HOST and SMTP_FROM_EMAIL)


def send_email(to_email: str, subject: str, body: str) -> bool:
    if not smtp_configured():
        return False

    msg = EmailMessage()
    msg["From"] = SMTP_FROM_EMAIL
    msg["To"] = to_email
    msg["Subject"] = subject
    msg.set_content(body)

    with smtplib.SMTP(SMTP_HOST, SMTP_PORT, timeout=20) as smtp:
        if SMTP_USE_TLS:
            smtp.starttls()
        if SMTP_USERNAME:
            smtp.login(SMTP_USERNAME, SMTP_PASSWORD)
        smtp.send_message(msg)

    return True

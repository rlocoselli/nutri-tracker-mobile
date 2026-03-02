import uuid
import hashlib
import hmac
import os
import secrets
from datetime import datetime, timedelta, timezone
from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from sqlalchemy import desc
from ..db import get_db
from ..config import AUTH_CODE_TTL_MINUTES, APP_COMPANY_NAME, APP_DATA_LOCATION
from ..mailer import send_email
from ..models import User, EmailAccount, EmailVerificationCode, PasswordResetCode
from ..schemas import (
    GoogleAuthIn,
    EmailRegisterIn,
    EmailLoginIn,
    EmailCodeVerifyIn,
    ForgotPasswordIn,
    ResetPasswordIn,
    ChangePasswordIn,
    DeleteAccountIn,
)
from ..security import get_current_user_id

router = APIRouter(prefix="/auth", tags=["auth"])


def _utcnow() -> datetime:
    return datetime.now(timezone.utc)


def _normalize_email(email: str) -> str:
    return (email or "").strip().lower()


def _hash_password(password: str, salt_bytes: bytes) -> str:
    return hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt_bytes, 120_000).hex()


def _new_password_hash(password: str) -> tuple[str, str]:
    salt = os.urandom(16)
    return _hash_password(password, salt), salt.hex()


def _verify_password(password: str, expected_hash: str, salt_hex: str) -> bool:
    try:
        salt = bytes.fromhex(salt_hex)
    except ValueError:
        return False
    actual = _hash_password(password, salt)
    return hmac.compare_digest(actual, expected_hash)


def _new_code() -> str:
    return f"{secrets.randbelow(1_000_000):06d}"


def _hash_code(code: str) -> str:
    return hashlib.sha256(code.encode("utf-8")).hexdigest()


def _validate_password_strength(password: str):
    if len(password or "") < 6:
        raise HTTPException(status_code=400, detail="Password must contain at least 6 characters")


def _send_verification_email(to_email: str, code: str) -> bool:
    subject = "NutritionTracker - Verification code"
    body = (
        "Bonjour,\n\n"
        f"Votre code de vérification est: {code}\n"
        f"Ce code expire dans {AUTH_CODE_TTL_MINUTES} minutes.\n\n"
        f"Données hébergées en {APP_DATA_LOCATION} par une entreprise française ({APP_COMPANY_NAME}).\n"
        "Si vous n'êtes pas à l'origine de cette demande, ignorez cet email."
    )
    return send_email(to_email, subject, body)


def _send_reset_email(to_email: str, code: str) -> bool:
    subject = "NutritionTracker - Reset password"
    body = (
        "Bonjour,\n\n"
        f"Votre code de réinitialisation est: {code}\n"
        f"Ce code expire dans {AUTH_CODE_TTL_MINUTES} minutes.\n\n"
        "Si vous n'êtes pas à l'origine de cette demande, ignorez cet email."
    )
    return send_email(to_email, subject, body)


@router.post("/google")
def auth_google(payload: GoogleAuthIn, db: Session = Depends(get_db)):
    fake_email = f"user-{payload.id_token[:8]}@example.com"
    existing = db.query(User).filter(User.email == fake_email).first()
    if existing:
        return {"user_id": str(existing.id), "email": existing.email, "token": "replace-with-real-jwt"}

    user = User(
        id=uuid.uuid4(),
        email=fake_email,
        google_sub=payload.id_token[:32],
        display_name="New User",
    )
    db.add(user)
    db.commit()
    db.refresh(user)

    return {"user_id": str(user.id), "email": user.email, "token": "replace-with-real-jwt"}


@router.post("/email/register")
def auth_email_register(payload: EmailRegisterIn, db: Session = Depends(get_db)):
    email = _normalize_email(payload.email)
    _validate_password_strength(payload.password)

    existing_user = db.query(User).filter(User.email == email).first()
    existing_email_account = db.query(EmailAccount).filter(EmailAccount.email_norm == email).first()
    if existing_user or existing_email_account:
        raise HTTPException(status_code=409, detail="Email already registered")

    user = User(
        id=uuid.uuid4(),
        email=email,
        google_sub=None,
        display_name=(payload.display_name or email.split("@")[0]).strip(),
    )
    password_hash, password_salt = _new_password_hash(payload.password)
    account = EmailAccount(
        user_id=user.id,
        email_norm=email,
        password_hash=password_hash,
        password_salt=password_salt,
        email_verified=False,
        created_at_utc=_utcnow(),
        updated_at_utc=_utcnow(),
    )

    code = _new_code()
    verification = EmailVerificationCode(
        email_norm=email,
        code_hash=_hash_code(code),
        expires_at_utc=_utcnow() + timedelta(minutes=AUTH_CODE_TTL_MINUTES),
        created_at_utc=_utcnow(),
    )

    db.add(user)
    db.add(account)
    db.add(verification)
    db.commit()

    mail_ok = _send_verification_email(email, code)
    if not mail_ok:
        return {"ok": True, "message": "Account created. SMTP not configured: verification email not sent."}

    return {"ok": True, "message": "Account created. Verification code sent by email."}


@router.post("/email/verify")
def auth_email_verify(payload: EmailCodeVerifyIn, db: Session = Depends(get_db)):
    email = _normalize_email(payload.email)
    code_hash = _hash_code((payload.code or "").strip())
    now = _utcnow()

    account = db.query(EmailAccount).filter(EmailAccount.email_norm == email).first()
    if not account:
        raise HTTPException(status_code=404, detail="Email account not found")

    record = (
        db.query(EmailVerificationCode)
        .filter(EmailVerificationCode.email_norm == email, EmailVerificationCode.consumed_at_utc.is_(None))
        .order_by(desc(EmailVerificationCode.created_at_utc))
        .first()
    )
    if not record or record.expires_at_utc < now or record.code_hash != code_hash:
        raise HTTPException(status_code=400, detail="Invalid or expired verification code")

    record.consumed_at_utc = now
    account.email_verified = True
    account.updated_at_utc = now
    db.commit()
    return {"ok": True, "message": "Email verified"}


@router.post("/email/login")
def auth_email_login(payload: EmailLoginIn, db: Session = Depends(get_db)):
    email = _normalize_email(payload.email)
    account = db.query(EmailAccount).filter(EmailAccount.email_norm == email).first()
    if not account:
        raise HTTPException(status_code=404, detail="Email account not found")

    if not account.email_verified:
        raise HTTPException(status_code=403, detail="Email not verified")

    if not _verify_password(payload.password or "", account.password_hash, account.password_salt):
        raise HTTPException(status_code=401, detail="Invalid credentials")

    user = db.query(User).filter(User.id == account.user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="User not found")

    return {
        "ok": True,
        "user_id": str(user.id),
        "email": user.email,
        "name": user.display_name or email.split("@")[0],
        "token": "replace-with-real-jwt",
        "auth_method": "email",
    }


@router.post("/email/password/forgot")
def auth_email_forgot_password(payload: ForgotPasswordIn, db: Session = Depends(get_db)):
    email = _normalize_email(payload.email)
    account = db.query(EmailAccount).filter(EmailAccount.email_norm == email, EmailAccount.email_verified.is_(True)).first()
    if not account:
        return {"ok": True, "message": "If the account exists, an email has been sent."}

    code = _new_code()
    record = PasswordResetCode(
        email_norm=email,
        code_hash=_hash_code(code),
        expires_at_utc=_utcnow() + timedelta(minutes=AUTH_CODE_TTL_MINUTES),
        created_at_utc=_utcnow(),
    )
    db.add(record)
    db.commit()

    mail_ok = _send_reset_email(email, code)
    if not mail_ok:
        return {"ok": True, "message": "If the account exists, an email has been sent (SMTP not configured)."}

    return {"ok": True, "message": "If the account exists, an email has been sent."}


@router.post("/email/password/reset")
def auth_email_reset_password(payload: ResetPasswordIn, db: Session = Depends(get_db)):
    email = _normalize_email(payload.email)
    _validate_password_strength(payload.new_password)
    now = _utcnow()

    account = db.query(EmailAccount).filter(EmailAccount.email_norm == email).first()
    if not account:
        raise HTTPException(status_code=404, detail="Email account not found")

    code_hash = _hash_code((payload.code or "").strip())
    record = (
        db.query(PasswordResetCode)
        .filter(PasswordResetCode.email_norm == email, PasswordResetCode.consumed_at_utc.is_(None))
        .order_by(desc(PasswordResetCode.created_at_utc))
        .first()
    )
    if not record or record.expires_at_utc < now or record.code_hash != code_hash:
        raise HTTPException(status_code=400, detail="Invalid or expired reset code")

    record.consumed_at_utc = now
    new_hash, new_salt = _new_password_hash(payload.new_password)
    account.password_hash = new_hash
    account.password_salt = new_salt
    account.updated_at_utc = now
    db.commit()

    return {"ok": True, "message": "Password updated"}


@router.post("/email/password/change")
def auth_email_change_password(
    payload: ChangePasswordIn,
    current_user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    _validate_password_strength(payload.new_password)

    account = db.query(EmailAccount).filter(EmailAccount.user_id == current_user_id).first()
    if not account:
        raise HTTPException(status_code=404, detail="Email account not found")

    if not _verify_password(payload.current_password or "", account.password_hash, account.password_salt):
        raise HTTPException(status_code=401, detail="Current password is invalid")

    new_hash, new_salt = _new_password_hash(payload.new_password)
    account.password_hash = new_hash
    account.password_salt = new_salt
    account.updated_at_utc = _utcnow()
    db.commit()
    return {"ok": True, "message": "Password changed"}


@router.delete("/account")
def auth_delete_account(
    payload: DeleteAccountIn,
    current_user_id: uuid.UUID = Depends(get_current_user_id),
    db: Session = Depends(get_db),
):
    user = db.query(User).filter(User.id == current_user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="User not found")

    email_account = db.query(EmailAccount).filter(EmailAccount.user_id == current_user_id).first()
    if email_account:
        if not payload.password:
            raise HTTPException(status_code=400, detail="Password required for email accounts")
        if not _verify_password(payload.password, email_account.password_hash, email_account.password_salt):
            raise HTTPException(status_code=401, detail="Invalid password")

    db.delete(user)
    db.commit()
    return {"ok": True, "message": "Account and related data deleted"}

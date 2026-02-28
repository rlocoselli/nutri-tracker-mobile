import uuid
from fastapi import Header, HTTPException


def get_current_user_id(x_user_id: str | None = Header(default=None)) -> uuid.UUID:
    if not x_user_id:
        raise HTTPException(status_code=401, detail="Missing X-User-Id header")

    try:
        return uuid.UUID(x_user_id)
    except ValueError as exc:
        raise HTTPException(status_code=401, detail="Invalid X-User-Id") from exc

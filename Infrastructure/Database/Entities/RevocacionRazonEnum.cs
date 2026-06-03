using System;

namespace tmr_backend.Infrastructure.Database.Entities;

public enum RevocacionRazonEnum
{
    LOGOUT,
    ADMIN_REVOKE,
    PASSWORD_CHANGE,
    SECURITY_BREACH,
    SESSION_LIMIT,
    SESSION_IDLE_TIMEOUT,
    SESSION_EXPIRED,
    REVOKE,
    ADMIN_REVOKE_ALL,
    USER_AGENT_MISMATCH,
    IP_MISMATCH,
    TOKEN_REUSED,
    TOKEN_REVOKED,
    EXPIRED
}

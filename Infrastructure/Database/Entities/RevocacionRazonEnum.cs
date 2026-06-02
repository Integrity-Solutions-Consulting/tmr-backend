using System;

namespace tmr_backend.Infrastructure.Database.Entities;

public enum RevocacionRazonEnum
{
    LOGOUT,
    ADMIN_REVOKE,
    PASSWORD_CHANGE,
    SECURITY_BREACH,
    EXPIRED
}

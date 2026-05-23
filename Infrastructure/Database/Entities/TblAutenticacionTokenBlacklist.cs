using System;
using System.Collections.Generic;

namespace tmr_backend.Infrastructure.Database.Entities;

public partial class TblAutenticacionTokenBlacklist
{
    public long Id { get; set; }

    public string Token { get; set; } = null!;

    public DateTime? Fechaexpiracion { get; set; }

    public bool Activo { get; set; }

    public string Usuariocreacion { get; set; } = null!;

    public DateTime Fechacreacion { get; set; }
}

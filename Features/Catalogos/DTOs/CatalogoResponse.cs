namespace tmr_backend.Features.Catalogos.DTOs;

public sealed class CatalogoResponse
{
    public int Id { get; init; }
    public string Codigovalor { get; init; } = string.Empty;
    public string Valor { get; init; } = string.Empty;

    public CatalogoResponse(int id, string codigovalor, string valor)
    {
        Id = id;
        Codigovalor = codigovalor;
        Valor = valor;
    }
}

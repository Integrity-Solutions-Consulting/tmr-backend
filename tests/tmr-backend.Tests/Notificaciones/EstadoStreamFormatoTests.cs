using System.Net.ServerSentEvents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using tmr_backend.Features.Notificaciones.Presentation.Endpoint;

namespace tmr_backend.Tests.Notificaciones;

/// <summary>
/// El frontend (notificaciones.service.ts) lee los eventos de /estado/stream con estos nombres y campos.
/// </summary>
public class EstadoStreamFormatoTests
{
    [Fact]
    public async Task Los_eventos_salen_con_su_nombre_y_el_json_en_camelCase()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.ConfigureHttpJsonOptions(_ => { });
        var http = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        var cuerpo = new MemoryStream();
        http.Response.Body = cuerpo;

        await TypedResults.ServerSentEvents(Eventos()).ExecuteAsync(http);

        var texto = System.Text.Encoding.UTF8.GetString(cuerpo.ToArray());
        Assert.Equal("text/event-stream", http.Response.ContentType);
        Assert.Contains("event: estado", texto);
        Assert.Contains("\"idEmpleado\":5", texto);
        Assert.Contains("\"estado\":\"Enviado\"", texto);
        Assert.Contains("event: fin", texto);
    }

    private static async IAsyncEnumerable<SseItem<EstadoEmpleadoDto?>> Eventos()
    {
        yield return new SseItem<EstadoEmpleadoDto?>(new EstadoEmpleadoDto(5, "Enviado", null), "estado");
        await Task.Yield();
        yield return new SseItem<EstadoEmpleadoDto?>(null, "fin");
    }
}

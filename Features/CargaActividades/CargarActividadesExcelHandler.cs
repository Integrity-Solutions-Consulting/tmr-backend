using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs; 
using tmr_backend.Infrastructure.Database;
using tmr_backend.Features.CargaActividades.Domain;

namespace tmr_backend.Features.CargaActividades
{
    public interface ICargarActividadesExcelHandler
    {
        Task<CargaActividadesResponse> HandleAsync(CargarActividadesExcelCommand command, CancellationToken cancellationToken = default);
    }

    public class CargarActividadesExcelHandler : ICargarActividadesExcelHandler
    {
        private readonly ApplicationDbContext _context;

        public CargarActividadesExcelHandler(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<CargaActividadesResponse> HandleAsync(CargarActividadesExcelCommand command, CancellationToken cancellationToken = default)
        {
            var erroresValidacion = new List<string>();
            var actividadesParaInsertar = new List<Actividad>();
            var listaParaFrontend = new List<object>();

            try
            {
                // 1. LEER ARCHIVO: Abrimos de forma segura el stream binario
                using var streamArchivo = command.File.OpenReadStream();

                // Consultamos el archivo de forma directa. MiniExcel infiere las cabeceras automáticamente
                // al mapear el renglón hacia un Diccionario de string y object.
                var datosCrudosExcel = streamArchivo.Query(useHeaderRow: true);

                // 2. PROCESAMIENTO Y PROYECCIÓN DE DATOS
                foreach (dynamic fila in datosCrudosExcel)
                {
                    // Normalizamos las llaves del diccionario para evitar problemas de case-sensitivity (Mayúsculas/Minúsculas)
                    var filaDict = (IDictionary<string, object>)fila;
                    var filaNormalizada = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var kvp in filaDict)
                    {
                        filaNormalizada[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
                    }
                    // Extraemos los campos indispensables para el dominio transaccional
                    string nombreActividad = filaNormalizada.ContainsKey("Nombre") ? filaNormalizada["Nombre"] : 
                                            (filaNormalizada.ContainsKey("Descripcion") ? filaNormalizada["Descripcion"] : "Actividad General");
                    string descripcionActividad = filaNormalizada.ContainsKey("Descripcion") ? filaNormalizada["Descripcion"] : "Procesado masivamente desde planilla";

                    // Regla de Negocio: Omitir registros o líneas completamente vacías
                    if (string.IsNullOrWhiteSpace(nombreActividad)) continue;

                    // Instanciamos el objeto de dominio e impactamos la lista interna para persistencia
                    var actividadDominio = Actividad.Crear(nombreActividad, descripcionActividad);
                    actividadesParaInsertar.Add(actividadDominio);

                    // --- FORMATO REQUERIDO POR EL MODELO TS DE NG-RX (FRONTEND) ---
                    string colaborador = filaNormalizada.ContainsKey("Colaborador") ? filaNormalizada["Colaborador"] : "Jeremy Flores";
                    string proyecto = filaNormalizada.ContainsKey("Proyecto") ? filaNormalizada["Proyecto"] : "Carga de Actividades TMR";
                    string cliente = filaNormalizada.ContainsKey("Cliente") ? filaNormalizada["Cliente"] : "Corporativo Global";
                    string lider = filaNormalizada.ContainsKey("LiderTecnico") ? filaNormalizada["LiderTecnico"] : "Supervisor Backend";
                    
                    double horas = 8.0; // Valor por defecto seguro
                    if (filaNormalizada.ContainsKey("NroHoras") && double.TryParse(filaNormalizada["NroHoras"], out var h))
                    {
                        horas = h;
                    }
                    else if (filaNormalizada.ContainsKey("Horas") && double.TryParse(filaNormalizada["Horas"], out var h2))
                    {
                        horas = h2;
                    }

                    // Construimos la proyección anónima con las propiedades exactas que la UI necesita renderizar
                    listaParaFrontend.Add(new
                    {
                        id = actividadDominio.Id.ToString(),
                        colaborador = colaborador,
                        proyecto = proyecto,
                        cliente = cliente,
                        liderTecnico = lider,
                        nroHoras = horas,
                        estado = "Cargado" // Satisface el Badge de estado en tu HTML
                    });
                }

                // 3. PERSISTENCIA EN EL PROVEEDOR EN MEMORIA (Simulación Base de Datos)
                if (actividadesParaInsertar.Count > 0)
                {
                    await _context.Actividades.AddRangeAsync(actividadesParaInsertar, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);
                }

                // 4. RESPUESTA ENRIQUECIDA PARA SINK EN NG-RX
                return CargaActividadesResponse.Success(
                    actividadesParaInsertar.Count, 
                    "La planilla de actividades se ha procesado y sincronizado con éxito.",
                    listaParaFrontend
                );
                
                // Asignamos el payload mapeado a la propiedad extendida del DTO de respuesta, para que el frontend pueda consumirlo directamente sin necesidad de transformaciones adicionales.    
            }
            catch (Exception ex)
            {
                return CargaActividadesResponse.Failure($"Error crítico en el lote simulado de desarrollo: {ex.Message}");
            }
        }
    }
}
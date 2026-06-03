// CargaActividadesResponse.cs
using System.Collections.Generic;

namespace tmr_backend.Features.CargaActividades
{
    public class CargaActividadesResponse
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public int RegistrosProcesados { get; set; }
        public List<string> ErroresValidacion { get; set; } = new List<string>();
        
        // AGREGAR ESTA PROPIEDAD: Para que coincida con el frontend
        public List<object> Actividades { get; set; } = new List<object>();

        public static CargaActividadesResponse Success(int registrosProcesados, string mensaje = "Archivo procesado con éxito.", List<object>? actividades = null)
        {
            return new CargaActividadesResponse 
            { 
                IsSuccess = true, 
                Message = mensaje, 
                RegistrosProcesados = registrosProcesados,
                Actividades = actividades ?? new List<object>()
            };
        }

        public static CargaActividadesResponse Failure(string mensaje, List<string>? errores = null)
        {
            return new CargaActividadesResponse 
            { 
                IsSuccess = false, 
                Message = mensaje, 
                RegistrosProcesados = 0,
                ErroresValidacion = errores ?? new List<string>()
            };
        }
    }
}
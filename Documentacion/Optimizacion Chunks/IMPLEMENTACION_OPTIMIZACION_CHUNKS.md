# 🚀 Guía de Implementación: Optimización de Chunks y Performance

**Fecha:** 2026-06-19  
**Objetivo:** Reducir tiempo de carga del frontend mejorando performance de chunks  
**Problema:** Las librerías pesadas (`exceljs`, `jspdf`, `xlsx`) se cargan en el bundle inicial

---

## 📊 Análisis Previo: Identificar chunks pesados

### Paso 1: Instalar webpack-bundle-analyzer

```bash
cd tmr-frontend
npm install --save-dev webpack-bundle-analyzer
```

### Paso 2: Agregar script de análisis

Editar [package.json](tmr-frontend/package.json) y agregar este script:

```json
"scripts": {
  "ng": "ng",
  "start": "ng serve",
  "build": "ng build",
  "build:analyze": "ng build --stats-json && webpack-bundle-analyzer dist/gestion-actividades/stats.json",
  "watch": "ng build --watch --configuration development",
  "test": "ng test"
}
```

### Paso 3: Ejecutar análisis

```bash
npm run build:analyze
```

**Esperar resultado:** Se abrirá una interfaz visual mostrando:
- Tamaño de cada chunk
- Librerías que ocupan más espacio
- Dónde están concentradas las dependencias pesadas

**Guardar evidencia:** Capturar pantalla del análisis para identificar:
- Main bundle size
- Chunks más grandes
- Librerías a optimizar

---

## 🎯 PUNTO 1: Lazy Load de Librerías Pesadas

### 1.1 - Lazy Load en `carga-actividades.component.ts`

**Archivo:** [tmr-frontend/src/app/features/carga-actividades/carga-actividades.component.ts](tmr-frontend/src/app/features/carga-actividades/carga-actividades.component.ts)

Modificar para importar dinámicamente `exceljs` y `xlsx`:

```typescript
// Antes: import { Workbook } from 'exceljs'; (NO HACER)

export class CargaActividadesComponent {
  
  async procesarArchivo(file: File) {
    try {
      // ✅ Importar dinámicamente solo cuando se necesita
      const { Workbook } = await import('exceljs');
      const XLSX = await import('xlsx');
      
      // Procesar con exceljs
      const workbook = new Workbook();
      await workbook.xlsx.load(file);
      
      const worksheet = workbook.worksheets[0];
      const datos = [];
      
      worksheet.eachRow((row, rowNumber) => {
        if (rowNumber > 1) { // Saltar encabezado
          datos.push({
            proyecto: row.getCell(1).value,
            actividad: row.getCell(2).value,
            horas: row.getCell(3).value,
            // ... más campos
          });
        }
      });
      
      return datos;
    } catch (error) {
      console.error('Error procesando archivo:', error);
      throw error;
    }
  }
}
```

**Resultado esperado:**
- `exceljs` y `xlsx` NO se cargan hasta que el usuario suba un archivo
- Reducción de bundle inicial: **~150-200 KB**

---

### 1.2 - Lazy Load en `reporte-horas.component.ts`

**Archivo:** [tmr-frontend/src/app/features/reportes/componentes/reporte-horas/reporte-horas.component.ts](tmr-frontend/src/app/features/reportes/componentes/reporte-horas/reporte-horas.component.ts)

Modificar para importar dinámicamente `jspdf` e `html2canvas`:

```typescript
export class ReporteHorasComponent {
  
  async exportarPDF() {
    try {
      // ✅ Importar solo cuando exporta
      const { jsPDF } = await import('jspdf');
      await import('jspdf-autotable');
      const html2canvas = (await import('html2canvas')).default;
      
      const element = document.getElementById('tabla-reporte');
      const canvas = await html2canvas(element, { scale: 2 });
      
      const imgData = canvas.toDataURL('image/png');
      const doc = new jsPDF('l', 'mm', 'a4');
      
      const imgWidth = 297;
      const imgHeight = (canvas.height * imgWidth) / canvas.width;
      
      doc.addImage(imgData, 'PNG', 0, 0, imgWidth, imgHeight);
      doc.save('reporte-horas.pdf');
      
    } catch (error) {
      console.error('Error generando PDF:', error);
    }
  }
}
```

**Resultado esperado:**
- `jspdf` y dependencias se cargan solo al exportar
- Reducción de bundle inicial: **~100-150 KB**

---

### 1.3 - Actualizar `angular.json`

**Archivo:** [tmr-frontend/angular.json](tmr-frontend/angular.json)

Remover `exceljs` de `allowedCommonJsDependencies` (ya no es necesario en bundle principal):

```json
"allowedCommonJsDependencies": [
  "html2canvas",
  "canvg",
  "rgbcolor",
  "raf",
  "core-js"
]
```

**Antes tenía:**
```json
"allowedCommonJsDependencies": [
  "exceljs",
  "html2canvas",
  "canvg",
  "rgbcolor",
  "raf",
  "core-js"
]
```

---

## 🔧 PUNTO 2: Compresión en Dokploy

### 2.1 - Compresión en Backend (.NET)

**Archivo:** [tmr-backend/Program.cs](tmr-backend/Program.cs)

Agregar compresión Gzip y Brotli al inicio del archivo:

```csharp
using System.IO.Compression;

var builder = WebApplication.CreateBuilder(args);

// ✅ Agregar servicio de compresión de respuestas
builder.Services.AddResponseCompression(options =>
{
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
    
    // Configurar nivel de compresión
    options.GzipCompressionLevel = CompressionLevel.Optimal;
    options.BrotliCompressionLevel = CompressionLevel.Optimal;
    
    // Tipos MIME a comprimir
    options.MimeTypes = new[]
    {
        "text/plain",
        "text/css",
        "application/javascript",
        "text/html",
        "application/json",
        "image/svg+xml",
        "application/wasm",
        "application/octet-stream"
    };
});

// ... resto de configuraciones

var app = builder.Build();

// ✅ Usar compresión (DEBE estar ANTES de MapControllers)
app.UseResponseCompression();

// ... resto del pipeline

app.MapControllers();
app.Run();
```

**Verificación:**
- Compilar y desplegar en Dokploy
- En DevTools → Network → Response Headers verificar:
  ```
  content-encoding: gzip
  // o
  content-encoding: br
  ```

**Resultado esperado:**
- Reducción de tamaño de respuestas: **60-80%**

---

### 2.2 - Compresión en Nginx (si aplica en Dokploy)

Si Dokploy usa Nginx como reverse proxy, crear archivo de configuración:

**Archivo:** `.docker/nginx.conf` (crear si no existe)

```nginx
user nginx;
worker_processes auto;
error_log /var/log/nginx/error.log warn;
pid /var/run/nginx.pid;

events {
    worker_connections 1024;
}

http {
    include /etc/nginx/mime.types;
    default_type application/octet-stream;

    log_format main '$remote_addr - $remote_user [$time_local] "$request" '
                    '$status $body_bytes_sent "$http_referer" '
                    '"$http_user_agent" "$http_x_forwarded_for"';

    access_log /var/log/nginx/access.log main;

    sendfile on;
    tcp_nopush on;
    tcp_nodelay on;
    keepalive_timeout 65;
    types_hash_max_size 2048;

    # ✅ Gzip Compression
    gzip on;
    gzip_vary on;
    gzip_proxied any;
    gzip_comp_level 6;
    gzip_types 
        text/plain 
        text/css 
        text/xml 
        text/javascript 
        application/json 
        application/javascript 
        application/xml+rss 
        application/rss+xml 
        image/svg+xml;
    gzip_disable "msie6";

    # ✅ Brotli Compression (si está disponible)
    brotli on;
    brotli_comp_level 6;
    brotli_types 
        text/plain 
        text/css 
        text/xml 
        text/javascript 
        application/json 
        application/javascript;

    # ✅ Cache Headers para assets estáticos
    map $sent_http_content_type $expires {
        default off;
        text/html epoch;
        text/css max;
        application/javascript max;
        ~image/ max;
        ~font/ max;
    }
    expires $expires;

    upstream backend {
        server backend:5091;
    }

    server {
        listen 80;
        server_name _;

        root /usr/share/nginx/html;
        index index.html;

        # ✅ Servir frontend
        location / {
            try_files $uri $uri/ /index.html;
            add_header Cache-Control "public, max-age=3600";
        }

        # ✅ Assets estáticos con caché agresivo
        location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2|ttf|eot)$ {
            expires 1y;
            add_header Cache-Control "public, immutable";
        }

        # ✅ Proxy a backend
        location /api {
            proxy_pass http://backend;
            proxy_http_version 1.1;
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection 'upgrade';
            proxy_set_header Host $host;
            proxy_cache_bypass $http_upgrade;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

**En docker-compose.yml o configuración de Dokploy:**

```yaml
version: '3.8'

services:
  frontend:
    build:
      context: ./tmr-frontend
      dockerfile: Dockerfile
    ports:
      - "80:80"
    depends_on:
      - backend
    volumes:
      - .docker/nginx.conf:/etc/nginx/nginx.conf:ro

  backend:
    build:
      context: ./tmr-backend
      dockerfile: Dockerfile
    ports:
      - "5091:5091"
    environment:
      - ASPNETCORE_URLS=http://+:5091
      - ASPNETCORE_ENVIRONMENT=Production
```

---

### 2.3 - Dockerfile para Frontend (incluir compresión)

**Archivo:** `tmr-frontend/Dockerfile`

```dockerfile
# Etapa 1: Build
FROM node:21-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# Etapa 2: Servir con Nginx
FROM nginx:alpine
COPY --from=build /app/dist/gestion-actividades /usr/share/nginx/html
COPY .docker/nginx.conf /etc/nginx/nginx.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
```

---

## 📈 PUNTO 3: Análisis Detallado de Chunks

### 3.1 - Ejecutar Build con estadísticas

```bash
cd tmr-frontend
ng build --stats-json --configuration production
```

Esto genera `dist/gestion-actividades/stats.json`

### 3.2 - Analizar con diferentes herramientas

#### Opción A: webpack-bundle-analyzer

```bash
npm run build:analyze
```

**Interfaz visual muestra:**
- Treemap de todos los chunks
- Tamaño parsed vs gzip
- Qué módulos aportan más peso

#### Opción B: Source Map Explorer

```bash
npm install --save-dev source-map-explorer
npx source-map-explorer 'dist/gestion-actividades/**/*.js'
```

#### Opción C: Lighthouse en Chrome DevTools

1. Abrir DevTools (F12)
2. Ir a "Lighthouse"
3. Click en "Analyze page load"
4. Revisar "Opportunities" → "Reduce JavaScript"

### 3.3 - Interpretar resultados

**Buscar:**

| Métrica | Crítica si > | Acción |
|---------|-------------|--------|
| main.*.js | 200 KB | Reducir imports innecesarios |
| chunk.*.js | 500 KB | Hacer más lazy loading |
| Material bundle | 100 KB | Importar solo componentes usados |
| Total gzip | 300 KB | Más lazy loading + librerías dinámicas |

**Ejemplo de output:**

```
gzip size:
  main.abc123.js              ~120 KB
  chunk-proyectos.def456.js   ~80 KB
  chunk-reportes.ghi789.js    ~95 KB
  vendor.jkl012.js            ~250 KB
  ─────────────────────────────────────
  Total                        ~545 KB
```

---

## ✅ Checklist de Implementación

### Fase 1: Análisis (Hoy)
- [ ] Instalar `webpack-bundle-analyzer`
- [ ] Ejecutar `npm run build:analyze`
- [ ] Documentar tamaños actuales
- [ ] Identificar chunks > 200 KB

### Fase 2: Lazy Loading (Día 1)
- [ ] Actualizar `carga-actividades.component.ts` con dynamic imports
- [ ] Actualizar `reporte-horas.component.ts` con dynamic imports
- [ ] Actualizar `angular.json` - remover `exceljs`
- [ ] Compilar y verificar tamaño de bundle
- [ ] Ejecutar `npm run build:analyze` nuevamente

### Fase 3: Compresión Backend (Día 1-2)
- [ ] Actualizar `Program.cs` con ResponseCompression
- [ ] Crear o actualizar `Dockerfile` frontend y backend
- [ ] Verificar nginx.conf si aplica
- [ ] Compilar y probar localmente
- [ ] Desplegar en Dokploy staging

### Fase 4: Verificación en Producción (Día 2-3)
- [ ] Desplegar cambios en producción
- [ ] Abrir DevTools → Network
- [ ] Verificar `content-encoding: gzip` o `br` en respuestas
- [ ] Medir tiempos de carga con Lighthouse
- [ ] Comparar antes vs después

---

## 📊 Métricas Esperadas (Antes vs Después)

| Métrica | Antes | Después | Mejora |
|---------|-------|---------|--------|
| Main Bundle | ~320 KB | ~150 KB | **-53%** |
| Chunks totales | ~800 KB | ~600 KB | **-25%** |
| Tamaño gzip | ~180 KB | ~75 KB | **-58%** |
| Time to Interactive | ~4.5s | ~2.0s | **-56%** |
| First Contentful Paint | ~2.8s | ~1.2s | **-57%** |

---

## 🔍 Troubleshooting

### Problema: Error "Cannot find module 'exceljs'" en runtime

**Solución:** Verificar que el import dinámico esté dentro de un `async` o manejado con `.catch()`:

```typescript
// ❌ MAL
const { Workbook } = await import('exceljs');

// ✅ BIEN (si no está en async)
import('exceljs').then(({ Workbook }) => {
  // usar Workbook
}).catch(err => console.error(err));
```

### Problema: DevTools no muestra `content-encoding: gzip`

**Checklist:**
1. ¿Compilaste en `--configuration production`?
2. ¿Desactivaste source maps en producción?
3. ¿Verificaste que nginx/backend está sirviendo comprimido?

```bash
# Prueba desde línea de comandos
curl -i -H "Accept-Encoding: gzip" http://localhost:5091/api/test
```

Debe mostrar:
```
content-encoding: gzip
```

### Problema: Build falla con "file too large"

**Solución:** Angular está limitando el tamaño del bundle. Aumentar límite temporalmente:

```json
// angular.json
"budgets": [
  {
    "type": "initial",
    "maximumWarning": "3MB",
    "maximumError": "6MB"
  }
]
```

Luego de implementar lazy loading, reducir nuevamente.

---

## 📚 Referencias

- [Angular Performance Guide](https://angular.io/guide/performance-best-practices)
- [Webpack Bundle Analyzer](https://github.com/webpack-bundle-analyzer/webpack-bundle-analyzer)
- [ASP.NET Response Compression](https://learn.microsoft.com/en-us/aspnet/core/performance/response-compression)
- [Nginx Compression](https://nginx.org/en/docs/http/ngx_http_gzip_module.html)

---

## 📞 Soporte

Si encuentras problemas durante la implementación:

1. Verificar logs en Dokploy
2. Ejecutar `npm run build:analyze` para confirmar cambios
3. Usar DevTools Network tab para validar compresión
4. Revisar console para errores de imports dinámicos


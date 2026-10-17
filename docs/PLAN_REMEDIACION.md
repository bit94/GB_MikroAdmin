# Plan de remediación — GB.MikroAdmin

**Auditoría:** 3 de octubre de 2026  
**Alcance:** solución .NET MAUI (`net10.0-android`, `net10.0-windows10.0.19041.0`), Core, transporte RouterOS, almacenamiento local, pruebas y CI. Revisión de solo lectura de la aplicación; este informe es el único artefacto añadido al repositorio.

## Resumen ejecutivo

La aplicación Windows **compila en una compilación incremental** (0 errores, 108 advertencias), pero el resultado de compilación limpia que reproduce el `-warnaserror` de CI **falla con 108 errores de XamlC**. Además, la suite de pruebas no compila: hay 1 error CS1503 en una llamada que pasa un `string` donde se requiere `CreatedHotspotUser`. Son dos bloqueos de CI verificados; mientras sigan presentes, CI no puede certificar el estado actual.

Los riesgos principales son: (1) el flujo de pruebas y la compilación estricta están rotos; (2) el historial local convierte cualquier error de lectura/deserialización en una lista vacía y luego puede sobrescribir lotes que contienen credenciales activas de acceso; (3) conviene aclarar y probar que la comprobación del certificado se aplique a la sesión RouterOS autenticada, no solo a un handshake previo.

Los análisis de NuGet no reportaron paquetes vulnerables en los tres proyectos. No se encontraron archivos `appsettings` ni `.env` rastreados por Git en la búsqueda efectuada. No se observaron defectos P0 confirmados.

## Estado de remediación (17 de octubre de 2026)

Este documento conserva el estado observado durante la auditoría. Los cambios aplicados después
se resumen aquí:

| ID | Estado | Qué se hizo |
|---|---|---|
| P1-1 | Resuelto | La prueba QR compila; la suite pasa completa (132 pruebas). |
| P1-2 | Resuelto | Bindings XAML tipados; reconstrucción limpia de Windows con `-warnaserror`: 0 errores y 0 advertencias. Android compila con el mismo tratamiento. |
| P1-3 | Resuelto | Escritura atómica con temporal y `.bak`, recuperación del respaldo y error sin sobrescribir ante un archivo ilegible. El respaldo bueno ya no se reemplaza por el principal dañado. |
| P2-1 | Pendiente de decisión | El histórico sigue siendo JSON local. Falta definir cifrado en reposo y política de retención; no se implementó para no fijar una clave o un formato sin decisión previa. |
| P2-2 | Resuelto | La validación se enlaza mediante `TikConnectionSetup.CertificateValidationCallback` al socket API-SSL real que autentica; exige nombre, cadena y revocación en línea. Se eliminó el handshake previo separado. |
| P2-3 | Resuelto | `QrPayloadFor` respeta `StatusUrl` (con `{codigo}` o como segmento) y prioriza SSID cuando ambos existen; hay pruebas del contrato. |
| P2-4 | Resuelto | `RouterWriteException` clasifica duplicado, conexión, autenticación, permisos y rechazo del router; `TicketBatchException` informa el motivo y los tickets no intentados. |

Pendiente fuera de esta remediación: prueba contra un router real (API-SSL, certificado de
confianza, alta/baja y lote parcial), ejecución en dispositivo Android, cifrado/retención del
histórico y decisión entre CA de confianza y *pinning*.

## Lo que está bien hecho

| Área | Evidencia | Qué preservar |
|---|---|---|
| Separación de lógica y UI | `tests/GB.MikroAdmin.Tests/GB.MikroAdmin.Tests.csproj:15-19`; `src/GB.MikroAdmin/GB.MikroAdmin.csproj:29-31` | Mantener Core independiente de MAUI para que las pruebas puedan ejecutarse en `net10.0`. |
| Higiene de credenciales del router | `src/GB.MikroAdmin/Services/RouterSettingsStore.cs:6-23`; `src/GB.MikroAdmin/ViewModels/LoginViewModel.cs:73-85,101-110` | Conservar SecureStorage y no repoblar la contraseña en la interfaz. |
| TLS en modo real | `src/GB.MikroAdmin.Core/Services/Tik4NetRouterTransport.cs:26-37`; `src/GB.MikroAdmin/Services/RouterCertificateValidator.cs:4-18` | Mantener el fallo cerrado, TLS obligatorio y validación de nombre/cadena; resolver el punto de unión con la sesión real sin relajar controles. |
| Operaciones del transporte probadas con dobles | `tests/GB.MikroAdmin.Tests/Tik4NetRouterTransportTests.cs:11-179` | Ampliar la cobertura existente de alta, baja, conexión y rechazo de certificado. |
| Lotes con límites y cancelación | `src/GB.MikroAdmin.Core/Services/HotspotProvisioningService.cs:31-35,45-90`; `src/GB.MikroAdmin/ViewModels/UsersViewModel.cs:158-229` | Preservar el máximo de lote, la generación previa de códigos, el avance y la conservación explícita de éxitos parciales. |
| Serialización de acceso concurrente | `src/GB.MikroAdmin/Services/Tik4NetSessionFactory.cs:34-75` | Mantener el semáforo alrededor de la conexión síncrona de tik4net. |
| CI con verificación de Windows | `.github/workflows/ci.yml:18-54` | Mantener trabajos separados para Core/pruebas y para la carga MAUI de Windows, además de `-warnaserror`. |

## Hallazgos priorizados

### P1 Alto

#### P1-1 — La suite de pruebas no compila

**Evidencia:** `tests/GB.MikroAdmin.Tests/TicketPdfWriterTests.cs:294-300`; `TicketBatchExport.QrPayloadFor` recibe `CreatedHotspotUser` en `src/GB.MikroAdmin.Core/Services/TicketExporter.cs:100-105`. Ejecución de `dotnet test ... -c Release`: error CS1503 en la línea 300 (`string` no se convierte a `CreatedHotspotUser`).

**Impacto:** el trabajo de CI `pruebas` falla antes de ejecutar las pruebas. Los errores de regresión en generación y exportación de tickets no quedan detectados.

**Remediación:**
1. Cambiar la invocación de la prueba para pasar el ticket completo del lote (o cambiar la API deliberadamente, si esa es la interfaz prevista).
2. Revisar las expectativas del caso sobre URL de estado frente al comportamiento deseado del QR; mantener casos para URL vacía, plantilla con `{codigo}` y URL sin plantilla.
3. Ejecutar `dotnet test tests/GB.MikroAdmin.Tests/GB.MikroAdmin.Tests.csproj -c Release` y confirmar que todas las pruebas se ejecutan.

**Criterio de aceptación:** `dotnet test` termina con código 0 y todas las pruebas, incluidas las de PDF/QR, pasan; el trabajo `pruebas` de CI llega a ejecutar el conjunto completo.

#### P1-2 — La compilación limpia falla con el tratamiento de advertencias de CI

**Evidencia:** `.github/workflows/ci.yml:52-54` compila con `-warnaserror`; `src/GB.MikroAdmin/GB.MikroAdmin.csproj:8-10` activa nullable y análisis de estilo. Una reconstrucción limpia Release para Windows con `-warnaserror` falla con **108 errores** XamlC: mayoritariamente XC0022 por bindings sin `x:DataType`, más XC0025 por bindings con `Source` no compilados. Por ejemplo, `src/GB.MikroAdmin/Views/GeneratePage.xaml:15-18,26-33` y `src/GB.MikroAdmin/Views/TicketHistoryPage.xaml:12-55`.

**Impacto:** el pipeline de CI falla en la compilación Windows en un checkout limpio. La compilación incremental sin `-warnaserror` ocultó el bloqueo como 108 advertencias; no debe tomarse como prueba de que CI pasa.

**Remediación:**
1. Añadir `x:DataType` correcto en páginas, plantillas y bindings de cada ViewModel/elemento; revisar los bindings con `Source` y habilitar su compilación solo con el tipo de origen correctamente anotado.
2. Evitar silenciar XC0022/XC0025 o retirar `-warnaserror` como arreglo; resolver las advertencias y conservar la comprobación estricta.
3. Repetir una reconstrucción limpia Release con `-warnaserror` y verificar exactamente el comando del workflow.

**Criterio de aceptación:** reconstrucción limpia Windows Release con `-warnaserror` termina con 0 errores y 0 advertencias; el job `compilar-windows` pasa en CI.

#### P1-3 — Un historial ilegible puede hacerse pasar por vacío y perder lotes recuperables

**Evidencia:** `src/GB.MikroAdmin/ViewModels/TicketHistoryStore.cs:62-83` captura cualquier excepción de lectura/deserialización y devuelve `[]`; `SaveAsync` lee ese valor y escribe la lista resultante en `:26-34`; `WriteAsync` reemplaza el archivo directamente en `:86-87`. El historial contiene los códigos creados (`TicketBatchExport.Tickets`, `src/GB.MikroAdmin.Core/Services/TicketExporter.cs:79-87`), necesarios para volver a entregar o imprimir credenciales de tickets existentes en el router.

**Impacto:** una corrupción, acceso denegado o JSON incompatible se presenta como “sin historial”. La siguiente escritura puede sustituir el archivo con solo el lote nuevo, haciendo irrecuperables los códigos previos desde la aplicación aunque las cuentas sigan activas.

**Remediación:**
1. Distinguir “archivo ausente” de “historial no legible”; propagar o mostrar un aviso persistente y bloquear escrituras destructivas cuando falle la lectura.
2. Escribir mediante archivo temporal y reemplazo atómico; conservar una copia de recuperación del último JSON válido antes de migrar o reemplazarlo.
3. Añadir pruebas para JSON malformado, IOException, compatibilidad de esquema y guardado posterior a un error de lectura.

**Criterio de aceptación:** ante lectura fallida, la operación informa el error, no declara el historial vacío y no modifica ni sobrescribe el archivo original; las pruebas demuestran la recuperación segura.

### P2 Medio

#### P2-1 — Los tickets de acceso se persisten en JSON sin protección adicional de la aplicación

**Evidencia:** `src/GB.MikroAdmin/ViewModels/TicketHistoryStore.cs:19-24,74-90` guarda y lee JSON con `File.ReadAllTextAsync` / `File.WriteAllTextAsync` bajo `FileSystem.AppDataDirectory`; el modelo guarda usuario y contraseña de cada ticket en `src/GB.MikroAdmin.Core/Services/HotspotProvisioningService.cs:5` y `src/GB.MikroAdmin.Core/Services/TicketExporter.cs:79-87`.

**Impacto:** cualquier acceso al perfil local con permisos suficientes puede leer códigos que aún habilitan el Hotspot. `AppDataDirectory` aporta ubicación de aplicación, pero el código revisado no documenta cifrado del historial ni una política de retención más allá del máximo de lotes.

**Remediación:**
1. Decidir y documentar la clasificación y vida útil de estas credenciales; reducir retención y permitir borrado/limpieza explícita.
2. Evaluar cifrado en reposo con una clave protegida por el almacén seguro de plataforma; diseñar recuperación/migración y no registrar los códigos.
3. Probar eliminación segura, migración y comportamiento ante falta de clave sin perder silenciosamente el historial.

**Criterio de aceptación:** política de retención documentada, almacenamiento con controles acordes a la sensibilidad, y pruebas para eliminación y recuperación.

#### P2-2 — El handshake de certificado no está enlazado al socket de la sesión tik4net

**Evidencia:** `src/GB.MikroAdmin/Services/RouterCertificateValidator.cs:7-19` crea y autentica un `TcpClient`/`SslStream` de comprobación. `src/GB.MikroAdmin/MauiProgram.cs:19-26` registra la comprobación como callback previa. `src/GB.MikroAdmin.Core/Services/Tik4NetRouterTransport.cs:31-37` ejecuta ese callback y luego abre otra sesión mediante `SessionFactory.OpenAsync`.

**Impacto:** la comprobación del certificado ocurre en una conexión distinta de la que realiza la autenticación/operaciones. El código del repositorio no prueba que tik4net aplique en su socket real las mismas comprobaciones de cadena, revocación y SAN. No se afirma una explotación confirmada; queda una frontera de seguridad por verificar en el comportamiento de la dependencia.

**Remediación:**
1. Confirmar mediante código o documentación de la versión desplegada de tik4net cómo valida el certificado en `ApiSsl`.
2. Si la validación no es equivalente, conectar un callback de certificado a la sesión autenticada real o usar un transporte que permita controlarla; evitar confiar únicamente en un preflight separado.
3. Añadir una prueba de integración controlada para cadena no confiable, nombre/SAN incorrecto y certificado válido en la conexión que envía credenciales.

**Criterio de aceptación:** evidencia reproducible de que el socket que autentica credenciales rechaza certificados no confiables, revocados y con nombre/SAN incorrecto; la conexión válida sigue funcionando.

#### P2-3 — La opción de URL de estado no se refleja en el payload QR

**Evidencia:** la UI ofrece “Consulta de estado” en `src/GB.MikroAdmin/Views/GeneratePage.xaml:36-42`; el QR se forma en `src/GB.MikroAdmin.Core/Services/TicketExporter.cs:95-105` y utiliza solo Wi-Fi o `ticket.Name`, sin leer `StatusUrl`. Tanto la salida HTML como la térmica usan ese método (`src/GB.MikroAdmin.Core/Services/TicketHtmlWriter.cs:57-60`; `src/GB.MikroAdmin.Core/Services/TicketPdfWriter.cs:214-220`). Las expectativas de URL con código aparecen en `tests/GB.MikroAdmin.Tests/TicketPdfWriterTests.cs:288-300`, aunque actualmente esa prueba no compila (P1-1).

**Impacto:** el usuario puede configurar una URL de estado y exportar un QR que no la abre ni incluye el código en ella. Los compradores no reciben el flujo anunciado por ese campo.

**Remediación:**
1. Acordar precedencia cuando coexisten URL y SSID (un solo QR no puede codificar a la vez el portal HTTP y una conexión Wi-Fi).
2. Implementar y validar la composición segura de URL, sustituyendo `{codigo}` o agregando el código como segmento según el contrato acordado.
3. Cubrir PDF y HTML con pruebas de escaneo del payload, además de URL nula/vacía y URL ya terminada en `/`.

**Criterio de aceptación:** las pruebas comprueban el contenido decodificado del QR según la regla de precedencia acordada y el campo visible coincide con su efecto real.

#### P2-4 — Los fallos individuales de creación del lote pierden el motivo original

**Evidencia:** `src/GB.MikroAdmin.Core/Services/HotspotProvisioningService.cs:68-80` captura cualquier excepción no cancelada y almacena solo el código; `TicketBatchException` expone códigos fallidos y construye un mensaje sin la excepción interna (`:87-88,110-115`).

**Impacto:** si RouterOS rechaza altas por permisos, duplicados, desconexión o límites, el usuario y el registro no distinguen el motivo. Se dificulta recuperar incidencias, especialmente en lotes parciales.

**Remediación:**
1. Conservar detalles seguros por código/resultado o agrupar los motivos, sin incluir contraseñas ni registrar secretos.
2. Separar fallos esperables (por ejemplo, duplicado) de errores de conectividad/protocolo; no ocultar cancelación.
3. Añadir pruebas de fallo parcial que verifiquen conteo, motivo comunicado y persistencia de los éxitos.

**Criterio de aceptación:** cada lote parcial comunica un diagnóstico útil y seguro, y las pruebas confirman que no se pierden los códigos creados.

## Plan por fases

### Fase 0 — Contención inmediata

1. Corregir P1-1 para restablecer el job de pruebas.
2. Resolver el conjunto XC0022/XC0025 y confirmar la compilación Windows estricta de P1-2.
3. Cambiar el manejo del historial para que una lectura fallida impida sobrescribir, cubriendo P1-3.

**Criterio de salida:** pruebas verdes, reconstrucción Windows limpia con `-warnaserror` sin avisos y prueba que demuestra que un historial ilegible permanece intacto tras intentar guardar.

### Fase 1 — Seguridad y protección de datos

1. Verificar/enlazar la validación TLS con la sesión que autentica (P2-2).
2. Definir retención y protección en reposo del historial de tickets (P2-1).

**Criterio de salida:** aceptación documentada de riesgo y pruebas de TLS y almacenamiento alineadas con el comportamiento final.

### Fase 2 — Red de seguridad de pruebas

1. Reparar primero las pruebas existentes y añadir cobertura de los casos de fallo de lectura/escritura del historial.
2. Añadir pruebas de creación parcial y contrato URL/QR.
3. Considerar agregar cobertura de línea/ramas al job de CI, con umbral inicial acordado y sin usarlo como sustituto de pruebas de comportamiento.

**Criterio de salida:** CI ejecuta y pasa los flujos críticos de credenciales, ticket parcial, exportación y transporte.

### Fase 3 — Consolidación funcional

1. Acordar e implementar el contrato del QR cuando se combina portal de estado y SSID (P2-3).
2. Hacer visibles los motivos de fallos de altas sin exponer secretos (P2-4).

**Criterio de salida:** contrato documentado y pruebas de extremo a extremo del payload y de resultados parciales.

### Fase 4 — Calidad sostenida

1. Mantener el job Windows en reconstrucción limpia con `-warnaserror`.
2. Mantener escaneo de vulnerabilidades NuGet para proyectos de aplicación, Core y pruebas.
3. Añadir a CI la comprobación de retención/protección local que se acuerde y revisar trimestralmente dependencias y superficies TLS.

**Criterio de salida:** rama principal en verde y controles repetibles en cada PR.

## Backlog consolidado

| ID | Hallazgo | Severidad | Fase | Esfuerzo | Dependencias |
|---|---|---:|---:|---:|---|
| P1-1 | Prueba QR no compila; suite bloqueada | P1 | 0, 2 | S | Ninguna |
| P1-2 | Rebuild CI falla por 108 warnings XamlC tratados como errores | P1 | 0 | M | Ninguna |
| P1-3 | Historial ilegible se trata como vacío y puede sobrescribirse | P1 | 0, 2 | M | Ninguna |
| P2-1 | Credenciales de tickets persistidas como JSON local | P2 | 1 | M | Política de retención y plataforma objetivo |
| P2-2 | Validación de certificado en conexión previa, separada de sesión real | P2 | 1 | M | Confirmar comportamiento de tik4net 4.0.0 |
| P2-3 | URL de estado no se usa en QR | P2 | 3 | M | Acordar precedencia URL/SSID |
| P2-4 | Errores de alta por ticket se reducen a código fallido | P2 | 3 | S | Definir mensaje/telemetría sin secretos |

## Riesgo de no actuar

- **P1-1 / P1-2:** CI continuará sin validar la suite y bloqueará las compilaciones Windows en una ejecución limpia; cambios pueden llegar sin una verificación funcional útil.
- **P1-3:** un JSON ilegible puede convertirse en pérdida permanente de los códigos históricos necesarios para recuperar accesos que todavía existen en el router.
- **P2-1:** una persona con acceso local al perfil de la aplicación podría obtener credenciales de tickets todavía válidas.
- **P2-2:** la confianza en el certificado del socket real de RouterOS seguiría sin demostrarse.
- **P2-3 / P2-4:** clientes no obtendrían el QR de consulta previsto y fallos parciales serían más costosos de diagnosticar.

## Anexo

### Comandos de verificación

```powershell
# Suite
 dotnet test tests/GB.MikroAdmin.Tests/GB.MikroAdmin.Tests.csproj -c Release --nologo -v q

# Rebuild limpio que reproduce la política de CI
 dotnet build src/GB.MikroAdmin/GB.MikroAdmin.csproj -c Release `
   -f net10.0-windows10.0.19041.0 -t:Rebuild --nologo -v:q -warnaserror

# Inventario de vulnerabilidades (los tres proyectos)
 dotnet list src/GB.MikroAdmin/GB.MikroAdmin.csproj package --vulnerable --include-transitive
 dotnet list src/GB.MikroAdmin.Core/GB.MikroAdmin.Core.csproj package --vulnerable --include-transitive
 dotnet list tests/GB.MikroAdmin.Tests/GB.MikroAdmin.Tests.csproj package --vulnerable --include-transitive
```

### Resultado observado durante la auditoría

- Aplicación, Core y pruebas: `dotnet list package --vulnerable --include-transitive` informó que no encontró paquetes vulnerables en los orígenes NuGet consultados.
- Suite: no ejecutó pruebas; compilación detenida por CS1503 en `TicketPdfWriterTests.cs:300`.
- Build Release Windows incremental: 0 errores, 108 advertencias.
- Rebuild Release Windows con `-warnaserror`: 108 errores XamlC, principalmente XC0022 y XC0025.
- No se encontraron archivos `appsettings` o `.env` rastreados en la búsqueda de Git. No se revisó historial completo para detectar secretos en commits antiguos.
- El job Android no se ejecutó localmente; su compilación e integración con el runtime/dispositivo quedan pendientes.

### Checklist antes de publicar

- [ ] Pruebas pasan localmente y en CI.
- [ ] Rebuild Windows limpio con `-warnaserror`: 0 errores y 0 advertencias.
- [ ] Compilación Android y ejecución en dispositivo o emulador, incluyendo SecureStorage y ciclo de vida.
- [ ] El certificado inválido se rechaza en el socket RouterOS que autentica la sesión.
- [ ] Historial corrupto/inaccesible no se sobrescribe y existe procedimiento de recuperación.
- [ ] Retención y protección del historial de códigos aprobadas.
- [ ] Alta, cancelación, fallo parcial, QR, CSV y PDF validados con un router de prueba.
- [ ] Cambios destructivos sobre credenciales existentes tienen confirmación explícita y pruebas de migración.

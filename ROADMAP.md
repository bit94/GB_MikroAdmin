# Hoja de ruta

Objetivo: administrar tickets de internet por tiempo de forma óptima, sencilla y rápida, con el
foco principal en la **administración diaria del router**.

Leyenda: `[x]` hecho · `[ ]` pendiente

---

## Fase 0 — Terreno firme

- [x] Arreglar el TFM del proyecto de pruebas (`NU1201`) y que `dotnet test` corra
- [x] Extraer modelos y lógica pura a `GB.MikroAdmin.Core` (`net10.0`)
- [x] `global.json` fijando el SDK 10
- [x] CI en GitHub Actions: compilación y pruebas en cada push
- [x] Reconstrucción limpia de Windows con `-warnaserror` sin advertencias
- [x] Histórico local con escritura atómica, respaldo y recuperación ante corrupción
- [x] Verificar que `*.user` no está trackeado (ya estaba ignorado)

## Fase 1 — Que el modo real funcione

- [x] `ToRouterOsTime` con soporte de semanas, días, horas, minutos y segundos
- [x] `RemoveUserAsync` resolviendo el `.id` en lugar de pasar el nombre
- [x] Quitar la reflexión de `Tik4NetRouterTransport` y usar tik4net tipado
- [x] `ParseTime` fallando de forma explícita en lugar de devolver cero
- [x] Corregir las rutas `//interfaces` y `//users` del menú
- [x] Eliminar `DashboardPage` y sus enlaces rotos
- [x] Validar el certificado en el mismo socket API-SSL que autentica (nombre, cadena y revocación)
- [ ] **Probar contra un router real** (api-ssl, puerto 8729, certificado de confianza).
      Pendiente: hoy solo hay modo simulado.

## Fase 2 — Tickets de verdad

- [x] Reintento ante colisión de códigos
- [x] Lote parcial que no pierde los códigos ya creados
- [x] Histórico local de lotes generados
- [x] Exportar el lote a CSV
- [x] PDF imprimible de tickets (código, perfil, duración, fecha)
- [x] Compartir el lote desde el móvil
- [x] Cancelación y progreso real durante la creación de un lote largo
- [x] Recordar la última cantidad, el último perfil, la red Wi-Fi y el precio usados
- [x] Reintentar códigos duplicados y abortar el lote ante fallos sistémicos conservando lo creado

## Fase 3 — Administración diaria

- [x] Separar `DashboardViewModel` en tres ViewModels
- [x] Refresco automático del tiempo restante cada 30 s
- [x] Indicador de router conectado y botón de desconexión
- [x] Avisos con severidad
- [x] Selección de fila y confirmación antes de borrados masivos o del histórico
- [x] Crear, editar y eliminar perfiles (velocidad y duración del ticket)
- [ ] Suspender o reactivar un ticket sin borrarlo
- [ ] Extender el tiempo de un ticket existente
- [ ] Añadir tiempo en bloque a los tickets de un perfil
- [ ] Buscar por rango de fechas de creación
- [ ] Ordenar la lista de tickets por columna (código, perfil, restante)

## Fase 4 — Control del proyecto

- [x] `CHANGELOG.md`
- [x] `ROADMAP.md`
- [ ] Convención de commits (`feat:`, `fix:`, `test:`, `chore:`) y commits atómicos
- [ ] Pruebas de los ViewModels (filtro, resumen, máquina de estados de la interfaz)
- [ ] Publicación de la aplicación para Windows y Android

## Fase 5 — Ideas a evaluar

- [ ] Portal cautivo para que el cliente vea su tiempo restante
- [ ] Varios perfiles de conexión guardados (varios routers o varios negocios)
- [ ] Alertas cuando un ticket esté por agotarse
- [ ] Registro de auditoría: quién creó y quién eliminó cada ticket

---

## Notas de operación

### Requisitos del router para el modo real

RouterOS debe tener `api-ssl` habilitado (puerto 8729 normalmente) y un certificado de confianza.
La aplicación **rechaza de forma cerrada** certificados autofirmados, revocados, vencidos o cuyo
nombre (SAN/CN) no coincida con el host, y aplica esa comprobación al mismo socket que autentica
las credenciales. Para IPs, el certificado debe incluir esa IP en su SAN.

```routeros
# Ejemplo de alta manual equivalente a lo que hace la aplicación
/ip hotspot user add name=123456 password=123456 profile=limitado limit-uptime=1h
```

Se recomienda además usar VPN (WireGuard/IPsec), firewall, y una cuenta con privilegios mínimos.
Nunca exponer la API normal (`8728`) a Internet.

### Cómo correr el proyecto

```powershell
dotnet restore
dotnet build GB.MikroAdmin.sln -f net10.0-windows10.0.19041.0
dotnet test tests/GB.MikroAdmin.Tests/GB.MikroAdmin.Tests.csproj
```

### Duración de los tickets

RouterOS no tiene un campo de "duración del ticket" en los perfiles de Hotspot, así que
GB.MikroAdmin la guarda en el comentario del perfil con el marcador
`GBM:voucher-duration=<duración>`. Si editas el comentario a mano, conserva ese marcador o el
perfil dejará de tener duración y no se podrán crear lotes con él.

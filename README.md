# GB.MikroAdmin

Aplicación .NET MAUI con MVVM para administrar tickets de internet por tiempo en un MikroTik
vía API. Incluye un **modo simulado** para trabajar en la interfaz sin router y un adaptador
`tik4net` centralizado.

## Estructura

| Proyecto | Marco | Contenido |
|---|---|---|
| `src/GB.MikroAdmin` | `net10.0-android`, `net10.0-windows` | Interfaz MAUI, ViewModels, almacenamiento seguro |
| `src/GB.MikroAdmin.Core` | `net10.0` | Modelos y lógica pura: conversión de tiempos RouterOS, generación de lotes, estados de expiración |
| `tests/GB.MikroAdmin.Tests` | `net10.0` | Pruebas de la lógica pura y del transporte con una sesión falsa |

La lógica vive en `Core` para que las pruebas corran rápido y sin depender de Android o Windows.

## Seguridad

Las credenciales se guardan únicamente en `SecureStorage`, nunca en código ni en preferencias
planas. El modo real exige API-SSL/TLS (normalmente el puerto `8729`). Configure en RouterOS un
certificado de confianza y `api-ssl`; no exponga la API normal (`8728`) a Internet. Restrinja el
servicio con firewall o VPN y use una cuenta con privilegios mínimos.

La validación se aplica al mismo socket API-SSL que autentica las credenciales: se exige una
cadena de confianza, un nombre (SAN/CN) que coincida con el host y consulta de revocación en
línea. Los certificados autofirmados, vencidos o con nombre incorrecto se rechazan de forma
cerrada. Para IPs, el certificado debe incluir esa IP en su SAN. Como defensa adicional se
recomienda usar VPN (WireGuard/IPsec), firewall, y permitir exclusivamente la CA corporativa.

## Ejecutar

```powershell
dotnet restore
dotnet build src/GB.MikroAdmin/GB.MikroAdmin.csproj -f net10.0-windows10.0.19041.0
dotnet test tests/GB.MikroAdmin.Tests/GB.MikroAdmin.Tests.csproj
```

Active **Modo simulado** en la pantalla de conexión para probar altas, bajas, perfiles e
interfaces sin tocar ningún equipo. El simulador calcula el consumo con el reloj real, así que el
tiempo restante de los tickets avanza de verdad.

En modo real, el alta se traduce a `/ip/hotspot/user/add` con `name`, `password`, `profile` y
`limit-uptime`, equivalente a:

```routeros
/ip hotspot user add name=123456 password=123456 profile=limitado limit-uptime=1h
```

## Duración de los tickets

RouterOS no tiene un campo de duración en los perfiles de Hotspot, así que GB.MikroAdmin la guarda
en el comentario del perfil con el marcador `GBM:voucher-duration=<duración>`. Al crear un perfil
desde la aplicación se escribe automáticamente. Si editas el comentario a mano, conserva ese
marcador: sin él el perfil no tendrá duración y no se podrán generar lotes con él.

Las duraciones se aceptan como `HH:MM:SS` o en formas cortas al estilo RouterOS (`7d`, `12h`,
`90m`). Internamente se descomponen en semanas, días, horas, minutos y segundos, porque RouterOS
no admite que una sola unidad desborde su ámbito.

## Documentación

- [`CHANGELOG.md`](CHANGELOG.md) — qué cambió y por qué.
- [`ROADMAP.md`](ROADMAP.md) — fases de trabajo, pendientes y notas de operación.

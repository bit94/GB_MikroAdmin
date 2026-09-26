# Registro de cambios

Las fechas siguen el formato `AAAA-MM-DD`. Los cambios se agrupan por fase de trabajo.

## [Sin publicar]

### Corregido — el modo real no habría funcionado

- **Eliminación de usuarios.** `RemoveUserAsync` enviaba el nombre del usuario en el parámetro
  `numbers` de `/ip/hotspot/user/remove`, que RouterOS interpreta como índice o rango. Ahora se
  resuelve el `.id` real del usuario antes de borrarlo.
- **Duración de los vouchers.** `ToRouterOsTime` emitía las horas totales (`168h` para 7 días) y
  descartaba los segundos. RouterOS no acepta que una sola unidad desborde su ámbito, así que
  ahora la duración se descompone en semanas, días, horas, minutos y segundos (`1w2d3h4m5s`).
- **Lectura de tiempos ilegible.** `ParseTime` devolvía `TimeSpan.Zero` en silencio ante un
  carácter desconocido, con lo que un límite corrupto hacía que el voucher pareciera ilimitado.
  Ahora lanza `FormatException`.
- **Navegación del menú.** `GoToAsync("//interfaces")` y `"//users"` apuntaban a rutas que no
  existían en el Shell, por lo que dos botones del menú de inicio no hacían nada. Las rutas se
  centralizan en `Views/Routes.cs`.
- **Campos sin destino.** `DashboardPage.xaml` enlazaba `NewName`, `NewPassword`, `Hours`,
  `BatchPrefix` y `AddUserCommand`, que no existían en su ViewModel. La página era inalcanzable
  y duplicaba las tres páginas reales; se eliminó.

### Corregido — calidad y pruebas

- **Las pruebas no se ejecutaban.** El proyecto de pruebas apuntaba a `net10.0` pero referenciaba
  un proyecto MAUI multi-target, lo que producía `NU1201` en la restauración. Las dos pruebas
  existentes nunca habían corrido.
- **Cobertura.** De 2 pruebas a 60, incluyendo conversión de tiempos RouterOS, marcador de
  duración en perfiles, generación de lotes, estados de expiración y el transporte real tik4net
  con una sesión falsa.

### Añadido

- **`GB.MikroAdmin.Core`** (`net10.0`): modelos y lógica pura separados del proyecto MAUI. Es lo
  que permite que las pruebas corran rápido y sin dependencias de Android o Windows.
- **Histórico de lotes** (`Histórico`): los códigos generados se guardan en el dispositivo, así
  que cerrar la aplicación ya no los pierde y se pueden consultar o reimprimir.
- **Recuento de estado** en la pantalla de vouchers: en uso, sin usar y agotados.
- **Refresco automático cada 30 s** en la lista de vouchers; antes el tiempo restante solo
  cambiaba al pulsar "Actualizar".
- **Indicador de router conectado** en todas las páginas y botón de desconexión.
- **Avisos con severidad** (información, éxito, advertencia, error) en lugar de una única
  propiedad `Message` que se sobreescribía.
- **Reintento ante colisión de códigos.** Un lote de 100 códigos de 6 dígitos ya no puede fallar
  porque el generador repita un valor.
- **`VoucherBatchException`**: si el router rechaza parte de un lote, los códigos ya creados se
  conservan en lugar de perderse.
- **Entrada de duración flexible** al crear perfiles: `01:00:00`, `7d`, `12h`, `90m`.
- **Modo simulado con consumo real**: el router en memoria calcula el tiempo transcurrido, así
  que la interfaz muestra avance sin tocar ningún equipo.
- **`global.json`** fijando el SDK 10.
- **CI** en GitHub Actions: compilación y pruebas en cada push y pull request.

### Cambiado

- **`Tik4NetRouterTransport`** usa tik4net tipado en lugar de `Type.GetType` + `dynamic`. La
  dependencia ya existía en el proyecto, así que la reflexión solo eliminaba IntelliSense y
  comprobación en compilación.
- **ViewModels separados** (`UsersViewModel`, `ProfilesViewModel`, `InterfacesViewModel`) en
  lugar de un único `DashboardViewModel` compartido por tres páginas.
- **Registro de dependencias**: los ViewModels de página pasan a ser singleton, porque
  `AppShell` crea cada página una sola vez y con `AddTransient` cada página tendría un ViewModel
  distinto del que la alimenta.
- **Estilos globales** en `App.xaml` y semántica de color por estado: verde disponible, ámbar en
  uso, rojo agotado.

# Registro de cambios

Las fechas siguen el formato `AAAA-MM-DD`. Los cambios se agrupan por fase de trabajo.

## [Sin publicar]

### Cambiado — de «vouchers» a «tickets»

- **Renombre completo del vocabulario.** La aplicación, el código, las pruebas y la
  documentación hablan ahora de *tickets*: `TicketPdfWriter`, `TicketExporter`,
  `TicketState`, `ITicketHistoryStore`, `TicketBatchExport`, etc. Hay dos excepciones
  deliberadas, porque son formatos ya persistidos y cambiarlos rompería instalaciones
  existentes: el marcador de duración en el comentario del perfil sigue siendo
  `GBM:voucher-duration=`, y el histórico local sigue leyendo el antiguo
  `voucher-history.json` (las nuevas escrituras van a `ticket-history.json`).

### Añadido — Fase 3: impresión con QR y seguimiento

- **Formato de ficha de mostrador.** La tarjeta impresa reproduce el modelo habitual: marca en
  cabecera, número de ticket, código en grande con una llave, perfil y precio, red Wi-Fi, QR,
  fecha de emisión y portal de consulta de estado. Solo se imprimen las líneas cuyos datos
  existen.
- **QR en cada ticket.** Se codifica el propio código, o la URL de consulta con el código
  incrustado —sustituyendo el marcador `{codigo}` si aparece, o añadido como último segmento de
  la ruta si no—. Se dibuja como rectángulos negros a partir de la matriz que calcula
  **QRCoder**, la única dependencia añadida; el PDF sigue escribiéndose a mano. El margen de
  silencio no se pinta: el fondo blanco de la tarjeta ya lo aporta y los módulos negros ocupan
  todo el espacio asignado, ~38% más grandes que con margen dibujado.
- **Hoja carta aprovechada al máximo: 4 × 6 tarjetas por cara** (24 tickets), con encabezado de
  contexto por página. Antes eran 3 × 9 en A4 y 3 × 5 en carta; la retícula actual prioriza el
  QR y los datos de venta sin renunciar a la densidad.
- **Ticket individual para impresora térmica de 80 mm** desde el histórico: la página del PDF
  mide exactamente lo que mide el ticket —ni un punto más—, porque en un rollo el sobrante se
  corta y se tira. Sin marco de recorte: el borde del papel ya separa el recibo. El botón
  «Térmico» imprime el siguiente ticket del lote que aún no se ha impreso.
- **Seguimiento de impresión por ticket.** `TicketBatchExport` conserva la lista de códigos ya
  enviados a imprimir y el histórico muestra «Impresos n/m» por lote. Los lotes guardados antes
  de este cambio se deserializan con la lista vacía, así siguen siendo legibles.
- **Datos de la tarjeta al generar el lote** (opcionales): marca, red Wi-Fi, precio y URL de
  consulta de estado. Se guardan con el lote y sobreviven a reimpresiones.

### Corregido — Fase 3

- **El PDF se abría en blanco.** El árbol de páginas listaba sus hijos como enteros sueltos
  (`/Kids [5]`) en vez de referencias (`/Kids [5 0 R]`). Los lectores reales interpretaban el
  entero como un objeto de tipo incorrecto y renderizaban una página vacía; la prueba nueva
  `Pdf_page_tree_kids_are_object_references` fija el formato para que no vuelva a pasar.

### Cambiado — rendimiento y mantenimiento

- **La interfaz ya no se congela al hablar con el router.** Solo la apertura de la conexión se
  apartaba del hilo de UI; los listados y los comandos corrían en él y bloqueaban la pantalla
  en cada refresco automático. Ahora todo se ejecuta en segundo plano, con un semáforo que
  serializa los comandos sobre la misma conexión, porque el refresco de 30 s y un lote en
  curso pueden solaparse.
- **"Limpiar agotados" en una sola pasada.** Antes cada borrado hacía un listado completo de
  usuarios solo para resolver su `.id`: con 100 agotados eran 100 listados. Ahora hay un único
  listado y un único `/ip/hotspot/user/remove numbers=id1,id2,...`. Los nombres que ya no
  existen se ignoran, porque el estado deseado —que no estén— ya se cumple.
- **`GB.MikroAdmin.sln` incluye Core y Tests.** Un build a nivel de solución solo compilaba la
  app MAUI; Core y Tests vivían fuera y solo se compilaban en CI.
- **Marcador de duración más tolerante.** `ProfileMarker` acepta notas escritas *antes* del
  marcador en el comentario del perfil, no solo después.

### Notas técnicas

- `ITikSession` pasa a ser asíncrona (`ListAsync`/`ExecuteAsync` con `CancellationToken`), así
  el transporte de Core no vuelve a caer en llamadas síncronas disfrazadas de `Task.FromResult`.
- `AsyncCommand<T>` expone `ExecutionFailed` como `AsyncCommand` y vive junto a él; el
  `async void` sin captura podía derribar el proceso.
- Se retiran propiedades sin uso fuera de `UsersViewModel` y el comando de borrar perfil, que
  no tenía implementación ni botón en la interfaz.

### Añadido — Fase 2: entrega de los tickets

- **Exportar a CSV.** Separado con punto y coma y no con coma, porque el destino habitual es
  Excel en configuración regional española, donde la coma es el separador decimal. El escapado
  sigue RFC 4180 para campos con separador, comillas o saltos de línea.
- **PDF imprimible de tickets.** El código se imprime a tamaño grande —es el dato que se
  dicta— y se encoge solo si no cabe en la tarjeta. El formato actual de la tarjeta y el reparto
  por hojas se describen en «Fase 3: impresión con QR y seguimiento».
- **Compartir el lote** con las aplicaciones del sistema (WhatsApp, correo, almacenamiento).
- **Reimprimir desde el histórico**, con los botones CSV, Reimprimir y Quitar por cada lote.
- **Progreso real durante la generación**: barra de avance y contador `n de m`.
- **Cancelar un lote a medias.** Un lote de 100 usuarios tarda minutos y antes no había forma de
  pararlo. Lo ya creado se conserva y se guarda en el histórico, porque esos usuarios existen en
  el router aunque el lote se interrumpa.

### Notas técnicas

- `TicketPdfWriter` escribe el PDF directamente en lugar de añadir una dependencia: el documento
  es una retícula de tarjetas con texto y las librerías habituales añaden varios megabytes a una
  aplicación móvil. Usa Helvetica, una de las catorce fuentes estándar que todo lector incorpora,
  así que no hay que empotrar ningún archivo. Las pruebas verifican las marcas del formato y que
  los desplazamientos de la tabla de referencias cruzadas apunten a cada objeto.
- `TicketBatchExport` vive en Core y sustituye al `TicketBatch` que estaba en los ViewModels:
  así el exportador y el escritor de PDF no dependen de MAUI y son verificables.
- `IFileExporter` abstrae guardar, compartir e imprimir. Imprimir abre el PDF con la aplicación
  predeterminada del sistema; desde ahí el usuario elige impresora, que es el camino que funciona
  en Android y Windows sin dependencias adicionales.

### Corregido — el modo real no habría funcionado

- **Eliminación de usuarios.** `RemoveUserAsync` enviaba el nombre del usuario en el parámetro
  `numbers` de `/ip/hotspot/user/remove`, que RouterOS interpreta como índice o rango. Ahora se
  resuelve el `.id` real del usuario antes de borrarlo.
- **Duración de los tickets.** `ToRouterOsTime` emitía las horas totales (`168h` para 7 días) y
  descartaba los segundos. RouterOS no acepta que una sola unidad desborde su ámbito, así que
  ahora la duración se descompone en semanas, días, horas, minutos y segundos (`1w2d3h4m5s`).
- **Lectura de tiempos ilegible.** `ParseTime` devolvía `TimeSpan.Zero` en silencio ante un
  carácter desconocido, con lo que un límite corrupto hacía que el ticket pareciera ilimitado.
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
- **Recuento de estado** en la pantalla de tickets: en uso, sin usar y agotados.
- **Refresco automático cada 30 s** en la lista de tickets; antes el tiempo restante solo
  cambiaba al pulsar "Actualizar".
- **Indicador de router conectado** en todas las páginas y botón de desconexión.
- **Avisos con severidad** (información, éxito, advertencia, error) en lugar de una única
  propiedad `Message` que se sobreescribía.
- **Reintento ante colisión de códigos.** Un lote de 100 códigos de 6 dígitos ya no puede fallar
  porque el generador repita un valor.
- **`TicketBatchException`**: si el router rechaza parte de un lote, los códigos ya creados se
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

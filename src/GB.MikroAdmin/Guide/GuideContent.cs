using GB.MikroAdmin.ViewModels;

namespace GB.MikroAdmin.Guide;

/// <summary>
/// Contenido de la guía de instalación. Se escribe como datos y no como XAML para poder
/// mantener las explicaciones, los comandos y los ejemplos en un solo lugar.
/// </summary>
public static class GuideContent
{
    public static IReadOnlyList<GuideChapter> Build() =>
    [
        BeforeStart(),
        Starlink(),
        RouterBase(),
        AccessPoints(),
        Hotspot(),
        CertificateAndApi(),
        AppAndTickets(),
        EndUser(),
        Checklist(),
        Troubleshooting(),
        Annexes(),
    ];

    private static GuideChapter BeforeStart() => new(
        "0",
        "Antes de empezar",
        "Inventario, plan de red y respaldo. Sin esto, el reset se vuelve prueba y error.",
        [
            Step("0.1", "Reúne el equipo",
                """
                Ten todo a mano antes de desconectar nada:
                • Antena y router Starlink, más el adaptador Ethernet (Gen 2) o el puerto LAN (Gen 3).
                • MikroTik hAP lite (RB941-2nD) con su fuente.
                • TP-Link EAP110-Outdoor con su inyector PoE.
                • Router TP-Link N (TL-WR840N, TL-WR941ND o similar) con su fuente.
                • Tres cables Ethernet: Starlink → hAP, hAP → EAP y hAP → TP-Link.
                • PC con Windows y WinBox; teléfono con Mikro Admin y la app de Starlink.
                • Etiquetas y plumón para no confundir cables.
                """,
                example:
                """
                Etiquetas sugeridas:
                ether1 = Starlink
                ether2 = EAP110 exterior
                ether3 = TP-Link interior
                ether4 = PC de administración
                """),
            Step("0.2", "Define nombres y contraseñas",
                """
                Vas a crear dos redes Wi-Fi y varios accesos. Decide los nombres ahora y anótalos
                en un gestor de contraseñas:
                • GB-Netlan: red abierta para clientes; el control lo hace el portal de tickets.
                • Mikro-Admin: red WPA2 solo para ti y para la app.
                • Usuario administrador del router y usuario de la API (son distintos).
                • Dominio del certificado, por ejemplo router.tudominio.com.

                No reutilices la contraseña del router en la API ni en el Wi-Fi de administración.
                """,
                example:
                """
                Mikro-Admin   → clave larga y única
                GB-Netlan     → abierta, sin clave
                admin router  → clave distinta a la de la API
                API           → usuario mikroadmin + clave única
                Dominio       → router.tudominio.com
                """),
            Step("0.3", "Fija el plan de direcciones",
                """
                La guía usa dos redes separadas para que un cliente del hotspot nunca llegue a la
                administración del router:
                • br-mgmt: 192.168.88.0/24. Router .1; DHCP .10 a .50; aquí viven tu PC y tu teléfono.
                • br-client: 10.10.0.0/24. Router .1; APs .2 y .3; pool del hotspot .100 a .200.
                • WAN: la que entrega Starlink por DHCP.

                Los APs llevan IP fija dentro de la red de clientes porque su cable llega al mismo
                puente que los clientes; más adelante se excluyen del portal.
                """,
                example:
                """
                192.168.88.1    hAP lite, administración
                192.168.88.10-50 pool de administración
                10.10.0.1       hAP lite, red de clientes
                10.10.0.2       EAP110-Outdoor
                10.10.0.3       TP-Link N
                10.10.0.100-200 pool de tickets
                """),
            Step("0.4", "Respalda lo que ya existe",
                """
                El reset borra la configuración del router y deja sin servicio a los tickets activos.
                Antes de empezar:
                • Exporta desde Histórico los lotes que necesites conservar (PDF o CSV).
                • Guarda un respaldo y un export del RouterOS actual.
                • Anota el correo y la contraseña de la cuenta Starlink.
                • Fotografía la configuración actual de los APs si ya están funcionando.
                """,
                code:
                """
                /export file=antes-de-reset
                /system backup save name=antes-de-reset
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: descarga los dos archivos desde Files en WinBox. El backup incluye claves y certificados; guárdalo en un lugar seguro."),
            Step("0.5", "Respeta el orden de trabajo",
                """
                Se configura de arriba hacia abajo, nunca al revés:
                1) Starlink entrega internet.
                2) hAP lite crea la red, el DHCP y el NAT.
                3) Los access points extienden el Wi-Fi.
                4) El hotspot autentica a los clientes.
                5) El certificado y la API permiten que la app administre el router.
                6) La app crea perfiles y tickets.
                7) Un cliente real prueba el recorrido completo.

                Si algo falla, vuelve al paso anterior antes de seguir. No configures la app antes
                de que exista api-ssl: solo perderás tiempo con errores de conexión.
                """),
            Step("0.6", "Reserva una ventana de mantenimiento",
                """
                Calcula entre 90 y 120 minutos para la primera instalación completa. Durante el
                reset no habrá internet en la zona. Avisa a tus clientes y evita hacerlo en hora
                pico.

                Si el router ya está en producción, ten a mano el respaldo del paso 0.4: puedes
                restaurarlo y volver al estado anterior si algo sale mal.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: los tickets activos no se migran solos. Al borrar los usuarios del hotspot dejan de existir, aunque les quede tiempo."),
        ]);

    private static GuideChapter Starlink() => new(
        "1",
        "Starlink: internet limpio",
        "Bypass o doble NAT, cable Ethernet y comprobación de la WAN.",
        [
            Step("1.1", "Actualiza la app y revisa la antena",
                """
                Abre la app de Starlink, inicia sesión y comprueba que la antena esté en línea.
                Deja que termine cualquier actualización pendiente y revisa el nivel de obstrucción.
                Una antena con obstrucciones provoca cortes que después parecerán fallos del hotspot.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: anota el correo de la cuenta Starlink. Lo necesitarás si tienes que volver a configurar el router."),
            Step("1.2", "Elige bypass o doble NAT",
                """
                Hay dos formas de conectar el hAP lite:
                • Bypass (recomendado): el router Starlink deja de enrutar y de emitir Wi-Fi; el
                  hAP lite recibe la IP de Starlink directamente por DHCP. Es la opción más limpia.
                • Doble NAT: el router Starlink sigue en modo normal y el hAP lite recibe una IP
                  192.168.1.x. Funciona para los tickets, pero suma una capa de NAT.

                Para este proyecto usa bypass si tienes adaptador Ethernet. Si todavía no lo tienes,
                deja el modo normal y cambia a bypass cuando lo consigas.
                """,
                example:
                """
                Bypass:    WAN del hAP = 100.64.x.x (CGNAT de Starlink)
                Doble NAT: WAN del hAP = 192.168.1.x
                """),
            Step("1.3", "Restablece el router Starlink",
                """
                Si vas a empezar de cero, restablece el router Starlink desde la app (opción de
                restablecimiento de fábrica) o con el botón de reset del equipo, según tu modelo.
                Espera a que reinicie y vuelve a conectarte.

                El reset del router no borra la cuenta ni el servicio; solo devuelve el equipo a
                su configuración inicial. Ten la contraseña de la cuenta a mano.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: no desconectes la antena durante una actualización. Puede tardar varios minutos en volver."),
            Step("1.4", "Conecta el cable hacia el hAP lite",
                """
                Conecta un cable Ethernet desde el puerto LAN del router Starlink hasta ether1 del
                hAP lite.
                • Gen 2: necesitas el adaptador Ethernet de Starlink.
                • Gen 3: usa cualquiera de sus puertos LAN.
                • Gen 1: el puerto Ethernet ya viene integrado.

                ether1 será siempre la entrada de internet (WAN). No la metas en el puente de clientes.
                """,
                example:
                """
                Starlink LAN  ─── cable ───  ether1 (hAP lite)
                """),
            Step("1.5", "Activa bypass o deja el modo normal",
                """
                En la app de Starlink entra a los ajustes del router y activa Bypass Mode si vas a
                usar la opción recomendada. El Wi-Fi del router Starlink se apaga y el hAP lite
                queda como único router de la red.

                Si eliges doble NAT, no actives bypass: deja el router Starlink como está y conecta
                el cable a su puerto LAN.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: para desactivar bypass necesitarás la app y la cuenta Starlink. Anota cómo volver atrás antes de cambiar el modo."),
            Step("1.6", "Comprueba el enlace antes de seguir",
                """
                Enciende el hAP lite y revisa que el LED de ether1 tenga enlace. Todavía no hay
                internet configurado en el MikroTik, así que es normal que los clientes no naveguen.
                La comprobación real de la WAN se hace en el capítulo 2, cuando el DHCP client esté
                funcionando.

                Si el LED de ether1 no enciende, cambia el cable o prueba otro puerto del Starlink.
                """),
            Step("1.7", "Mide la velocidad real y entiende el CGNAT",
                """
                Cuando el hAP tenga internet, haz una prueba de velocidad desde un equipo conectado
                a Mikro-Admin. El hAP lite tiene puertos de 100 Mbps, así que aunque Starlink entregue
                más, la red quedará limitada a unos 90 Mbps.

                Starlink usa CGNAT: la IP pública se comparte y no puedes recibir conexiones desde
                internet. Eso no afecta a los tickets (todo ocurre en tu red), pero sí impide validar
                un certificado por HTTP y acceder al router desde fuera.
                """,
                code:
                """
                /ip dhcp-client print
                /ip route print
                /ping 1.1.1.1 count=4
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: si la WAN muestra una IP 100.64.0.0/10, estás detrás de CGNAT. El capítulo 5 usa un certificado validado por DNS, que sí funciona en ese caso."),
        ]);

    private static GuideChapter RouterBase() => new(
        "2",
        "MikroTik hAP lite: la base",
        "Reset, actualización, dos puentes, DHCP, NAT y las dos Wi-Fi.",
        [
            Step("2.1", "Restablece el hAP lite",
                """
                Con el equipo apagado, mantén pulsado el botón reset, enciende y suelta cuando el
                LED empiece a parpadear. Eso carga la configuración de fábrica, que ya trae la
                192.168.88.1 y un bridge funcionando.

                Si prefieres hacerlo desde RouterOS y tienes acceso, el comando equivalente es el
                siguiente. El equipo se reinicia y pierde toda la configuración.
                """,
                code:
                """
                /system reset-configuration
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: la configuración de fábrica deja ether1 como WAN y ether2-5 más wlan1 en un bridge. Vamos a partir de esa base, no de cero absoluto."),
            Step("2.2", "Entra por ether4",
                """
                Conecta tu PC al puerto ether4 del hAP lite. Abre WinBox y en la pestaña Neighbors
                entra por la dirección MAC, o abre http://192.168.88.1 en el navegador.

                El usuario de fábrica es admin. Si el equipo es nuevo y trae contraseña impresa,
                usa la que aparece en la etiqueta.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: entra por cable y por ether4. Así no pierdes acceso cuando muevas ether2, ether3 y wlan1 al puente de clientes."),
            Step("2.3", "Actualiza RouterOS",
                """
                Actualiza antes de configurar. Una versión vieja puede no tener los comandos de
                certificados o de hotspot que usa esta guía.

                Hazlo por cable, con el equipo conectado a una fuente estable y sin interrumpir la
                energía. Después del reinicio, vuelve a entrar por ether4.
                """,
                code:
                """
                /system package update check-for-updates
                /system package update install
                /system resource print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: no actualices por Wi-Fi ni durante una tormenta. Un corte a mitad de la actualización puede dejar el router sin arrancar."),
            Step("2.4", "Asegura el acceso y ponle nombre",
                """
                Cambia la contraseña de admin y crea un usuario personal en el grupo full. No
                desactives admin hasta comprobar que el nuevo usuario puede entrar.

                El nombre del equipo ayuda a distinguirlo si algún día administras más de un router.
                """,
                code:
                """
                /system identity set name=hap-lite
                /user set admin password="UnaClaveLarga#2026"
                /user add name=gbautista group=full password="OtraClaveLarga#2026" comment="administrador"
                /user print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si desactivas admin sin haber probado el nuevo usuario, puedes quedarte fuera del router. Prueba primero en otra ventana de WinBox."),
            Step("2.5", "Hora, zona horaria y DNS",
                """
                El certificado TLS se valida contra la hora del equipo. Si el reloj está desfasado,
                la app rechazará una conexión que en realidad es correcta.

                Configura la zona horaria de tu país (el ejemplo usa México), sincroniza por NTP y
                deja que el router resuelva DNS para sus clientes.
                """,
                code:
                """
                /system clock set time-zone-name=America/Mexico_City
                /system ntp client set enabled=yes servers=time.cloudflare.com,pool.ntp.org
                /ip dns set servers=1.1.1.1,8.8.8.8 allow-remote-requests=yes
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: cambia America/Mexico_City por tu zona real. Puedes ver las disponibles con /system clock print."),
            Step("2.6", "Renombra el puente y libera los puertos de clientes",
                """
                La configuración de fábrica tiene un bridge llamado bridge con ether2, ether3,
                ether4, ether5 y wlan1. Ese bridge será la red de administración. Renómbralo y
                quita de él los puertos que van a los clientes: ether2, ether3 y wlan1.

                ether4 y ether5 se quedan en br-mgmt para tu PC y para un futuro switch de administración.
                """,
                code:
                """
                /interface bridge set [find name=bridge] name=br-mgmt
                /interface bridge port remove [find interface=ether2]
                /interface bridge port remove [find interface=ether3]
                /interface bridge port remove [find interface=wlan1]
                /interface bridge port print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si tu PC está en ether2 o ether3, muévela a ether4 antes de ejecutar estos comandos o perderás la conexión."),
            Step("2.7", "Crea el puente de clientes",
                """
                Crea br-client y añade ether2 (EAP exterior), ether3 (TP-Link interior) y wlan1
                (Wi-Fi del propio hAP). Todo lo que entre por este puente pasará por el hotspot.

                No agregues ether1: es la WAN y debe quedar fuera.
                """,
                code:
                """
                /interface bridge add name=br-client
                /interface bridge port add bridge=br-client interface=ether2
                /interface bridge port add bridge=br-client interface=ether3
                /interface bridge port add bridge=br-client interface=wlan1
                /interface bridge port print
                """),
            Step("2.8", "Asigna las direcciones IP",
                """
                br-mgmt conserva la 192.168.88.1/24 que traía la configuración de fábrica. Añade
                la 10.10.0.1/24 a br-client: será la puerta de enlace de los clientes y la dirección
                del portal.

                Comprueba que la 192.168.88.1 haya quedado en br-mgmt después del cambio de nombre.
                """,
                code:
                """
                /ip address print
                /ip address add address=10.10.0.1/24 interface=br-client
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si la 192.168.88.1 no aparece en br-mgmt, agrégala con /ip address add address=192.168.88.1/24 interface=br-mgmt antes de seguir."),
            Step("2.9", "Ajusta el DHCP de administración",
                """
                El DHCP de fábrica (defconf) sigue en br-mgmt. Reduce su pool a 192.168.88.10-50
                para dejar libres las IP fijas de los APs y del router.

                Verifica que la red DHCP entregue 192.168.88.1 como puerta de enlace y como DNS.
                """,
                code:
                """
                /ip pool print
                /ip pool set [find name=default-dhcp] ranges=192.168.88.10-192.168.88.50
                /ip dhcp-server print
                /ip dhcp-server network print
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: si tu versión usa otro nombre de pool, cámbialo en el comando según lo que muestre /ip pool print."),
            Step("2.10", "Crea el DHCP de clientes",
                """
                Los clientes del hotspot necesitan recibir IP por DHCP. Crea un pool exclusivo y un
                servidor sobre br-client. El portal usará ese mismo pool para asignar direcciones.

                La puerta de enlace y el DNS de los clientes son 10.10.0.1.
                """,
                code:
                """
                /ip pool add name=pool-client ranges=10.10.0.100-10.10.0.200
                /ip dhcp-server add name=dhcp-client interface=br-client address-pool=pool-client lease-time=1h disabled=no
                /ip dhcp-server network add address=10.10.0.0/24 gateway=10.10.0.1 dns-server=10.10.0.1
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: no crees una segunda red DHCP para 10.10.0.0/24. Si el comando dice que ya existe, revisa /ip dhcp-server network print."),
            Step("2.11", "Comprueba la WAN y el NAT",
                """
                ether1 debe pedir IP por DHCP a Starlink. La configuración de fábrica ya lo hace y
                también crea la regla de NAT; comprueba que sigan ahí después del reset.

                Si falta alguna, créala: sin NAT los clientes tendrán IP pero no navegarán.
                """,
                code:
                """
                /ip dhcp-client print
                /ip route print
                /ip firewall nat print
                /ip dhcp-client add interface=ether1 disabled=no
                /ip firewall nat add chain=srcnat out-interface=ether1 action=masquerade
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: si ya existe el DHCP client o la regla NAT, no ejecutes la línea de creación: tendrías duplicados."),
            Step("2.12", "Revisa el firewall y las listas de interfaces",
                """
                La configuración de fábrica protege la WAN y deja pasar la LAN. Añade br-client a la
                lista LAN para que los clientes puedan llegar al portal y al DNS del router.

                Más adelante bloquearemos desde br-client el acceso a los puertos de administración.
                """,
                code:
                """
                /interface list member add list=LAN interface=br-client
                /interface list member print
                /ip firewall filter print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: no agregues br-client a la lista WAN. La WAN es solo ether1."),
            Step("2.13", "Configura la Wi-Fi de clientes",
                """
                wlan1 será la red abierta GB-Netlan. No lleva contraseña: la autenticación la hace
                el portal del hotspot con el código del ticket.

                Usa 2.4 GHz, 20 MHz y el canal 1. Más adelante el EAP usará el 6 y el TP-Link el 11
                para no pisarse.
                """,
                code:
                """
                /interface wireless security-profiles add name=sec-open mode=none
                /interface wireless set wlan1 mode=ap-bridge ssid=GB-Netlan band=2ghz-b/g/n channel-width=20mhz frequency=2412 security-profile=sec-open country=mexico disabled=no
                /interface wireless print
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: cambia country=mexico por tu país. Si el comando falla, revisa el nombre exacto con /interface wireless print."),
            Step("2.14", "Configura la Wi-Fi de administración",
                """
                Crea un AP virtual (wlan2) sobre wlan1. Emitirá Mikro-Admin con WPA2 y pertenecerá
                a br-mgmt, así que tus equipos de administración no pasan por el hotspot.

                Es la red que usará la app para conectarse al router.
                """,
                code:
                """
                /interface wireless security-profiles add name=sec-admin mode=dynamic-keys authentication-types=wpa2-psk wpa2-pre-shared-key="ClaveAdminLarga#2026"
                /interface wireless add name=wlan2 master-interface=wlan1 ssid=Mikro-Admin security-profile=sec-admin disabled=no
                /interface bridge port add bridge=br-mgmt interface=wlan2
                /interface wireless print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: wlan2 comparte radio con wlan1. Ambas redes usarán el canal 1 del hAP; es normal y suficiente para administrar."),
            Step("2.15", "Comprueba la base antes del hotspot",
                """
                Conéctate a Mikro-Admin y confirma que recibes una IP 192.168.88.x, que responden
                la puerta de enlace y el DNS, y que navegas. Después conéctate a GB-Netlan: debes
                recibir una IP 10.10.0.x, todavía con internet libre porque el hotspot aún no existe.

                Si GB-Netlan no entrega IP, revisa los puertos del bridge y el DHCP de clientes.
                """,
                code:
                """
                /interface bridge port print
                /ip address print
                /ip dhcp-server lease print
                """),
            Step("2.16", "Guarda un respaldo de esta base",
                """
                Este es el mejor momento para guardar: la red base ya funciona y el hotspot todavía
                no añade complejidad. Guarda un backup y un export, y descárgalos del router.

                Si algo sale mal en los capítulos siguientes, puedes volver aquí sin repetir todo.
                """,
                code:
                """
                /system backup save name=base-ok
                /export file=base-ok
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: repite este respaldo al final de cada capítulo importante. Los backups no se sincronizan solos."),
        ]);

    private static GuideChapter AccessPoints() => new(
        "3",
        "Access points",
        "EAP110-Outdoor y TP-Link N en modo AP, con IP fija y canales separados.",
        [
            Step("3.1", "Restablece el EAP110-Outdoor",
                """
                Mantén pulsado el botón reset del EAP durante unos 10 segundos, hasta que los LED
                parpadeen. Espera a que arranque.

                Conecta el inyector PoE: el puerto LAN va a tu PC o al router y el puerto PoE va al
                access point. No conectes el PoE a la PC.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: usa el inyector incluido. Un PoE pasivo de otro equipo puede dañar el AP."),
            Step("3.2", "Entra a la configuración del EAP",
                """
                El EAP110-Outdoor sale de fábrica con la IP 192.168.0.254. Si tu PC no está en esa
                red, ponle una IP temporal 192.168.0.10/24 y abre http://192.168.0.254.

                Usa el modo Standalone (independiente), no un controlador Omada. Usuario y
                contraseña iniciales: admin / admin. Cámbialos al entrar.
                """,
                example:
                """
                PC temporal: 192.168.0.10 / 255.255.255.0
                EAP:         http://192.168.0.254
                Usuario:     admin
                Clave:       admin (cámbiala)
                """),
            Step("3.3", "IP fija y puerta de enlace del EAP",
                """
                El AP está conectado al mismo L2 que los clientes, así que su IP de administración
                debe estar en 10.10.0.0/24. Asigna 10.10.0.2, puerta de enlace 10.10.0.1 y DNS
                1.1.1.1.

                Desactiva cualquier DHCP del AP y no configures VLAN. El AP solo debe puentear el
                Wi-Fi hacia el cable.
                """,
                example:
                """
                IP:      10.10.0.2
                Máscara: 255.255.255.0
                Gateway: 10.10.0.1
                DNS:     1.1.1.1
                DHCP:    desactivado
                VLAN:    ninguna
                """),
            Step("3.4", "Configura el Wi-Fi del EAP",
                """
                En el EAP crea la red GB-Netlan, abierta y sin contraseña, en 2.4 GHz. Usa el
                canal 6 y 20 MHz. Desactiva el aislamiento de clientes y cualquier red de invitados
                que cree una segunda red.

                La autenticación la hará el portal del MikroTik; el AP solo transporta.
                """,
                example:
                """
                SSID:       GB-Netlan
                Seguridad:  ninguna (abierta)
                Banda:      2.4 GHz
                Canal:      6
                Ancho:      20 MHz
                Potencia:   máxima
                """),
            Step("3.5", "Conecta el EAP al hAP",
                """
                Lleva el cable del inyector PoE (puerto LAN) a ether2 del hAP lite. El EAP debe
                quedar alimentado y visible.

                Desde tu PC en Mikro-Admin, haz ping a 10.10.0.2. Todavía no hay hotspot, así que
                también puedes probar que un cliente conectado al EAP navega.
                """,
                code:
                """
                /ping 10.10.0.2 count=4
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: si no responde, revisa que el cable esté en ether2 y que el puerto del inyector sea el correcto (LAN, no PoE)."),
            Step("3.6", "Restablece el TP-Link N",
                """
                Mantén pulsado el reset del TP-Link durante unos 10 segundos. Conéctate a su puerto
                LAN, nunca al WAN, y abre su interfaz: 192.168.0.1, 192.168.1.1 o tplinkwifi.net,
                según el modelo.

                Si el router tiene un asistente con modo Access Point, elígelo. Si no, usa la
                configuración manual de los pasos siguientes.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: conecta el PC a un puerto LAN amarillo. El puerto WAN azul no sirve para administrar el equipo en modo AP."),
            Step("3.7", "Pon el TP-Link en modo AP",
                """
                En modo AP el router deja de repartir IP y de hacer NAT. Asigna la IP de
                administración 10.10.0.3/24, puerta de enlace 10.10.0.1 y desactiva su servidor
                DHCP. Guarda y reinicia.

                Si el equipo tiene un modo "Access Point" en el asistente, úsalo y luego verifica
                que la IP de administración sea 10.10.0.3.
                """,
                example:
                """
                IP LAN:   10.10.0.3
                Máscara:  255.255.255.0
                Gateway:  10.10.0.1
                DHCP:     desactivado
                WAN:      sin uso
                """),
            Step("3.8", "Configura el Wi-Fi del TP-Link",
                """
                Crea la misma red GB-Netlan, abierta, en 2.4 GHz, canal 11 y 20 MHz. No actives
                WDS ni aislamiento de clientes.

                Al usar el mismo nombre que el EAP, los clientes ven una sola red y se conectan a
                la señal más fuerte. Los canales distintos evitan que los APs se interfieran.
                """,
                example:
                """
                SSID:      GB-Netlan
                Seguridad: ninguna
                Canal:     11
                Ancho:     20 MHz
                """),
            Step("3.9", "Conecta el TP-Link al hAP",
                """
                Conecta un puerto LAN del TP-Link a ether3 del hAP lite. No uses el puerto WAN.
                Comprueba que responde en 10.10.0.3 desde la red de administración.

                A partir de aquí los dos APs extienden el Wi-Fi, pero todavía sin portal.
                """,
                code:
                """
                /ping 10.10.0.3 count=4
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si conectas el cable al WAN del TP-Link, los clientes quedarán detrás de otro NAT y el hotspot no los verá."),
            Step("3.10", "Ordena los canales y revisa la cobertura",
                """
                Plan de canales 2.4 GHz: hAP en 1, EAP en 6 y TP-Link en 11. Los tres son los
                únicos canales que no se solapan. Mantén 20 MHz: a 40 MHz en 2.4 GHz se pisa más
                de lo que se gana.

                Prueba la señal en los puntos donde se venden los tickets y donde se conectan los
                clientes. Si hay mucha interferencia, baja la potencia de los APs que estén muy
                cerca en lugar de subirla.
                """,
                example:
                """
                hAP lite   → canal 1
                EAP110     → canal 6
                TP-Link N  → canal 11
                """),
            Step("3.11", "Anota las IPs para excluirlas del portal",
                """
                Las IPs 10.10.0.2 y 10.10.0.3 son de administración, no de clientes. Más adelante
                las marcaremos como bypass en el hotspot para poder actualizar los APs sin comprar
                un ticket.

                No las metas en el pool 10.10.0.100-200.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: si algún día agregas otro AP, dale una IP fija fuera del pool (por ejemplo 10.10.0.4) y añade su bypass."),
        ]);

    private static GuideChapter Hotspot() => new(
        "4",
        "Hotspot y portal",
        "El portal que pide el código del ticket, con bypass para los APs.",
        [
            Step("4.1", "Crea el perfil del hotspot",
                """
                El perfil define la puerta de enlace, el nombre del portal y los métodos de login.
                Usa http-chap y http-pap: son los que funcionan con el formulario web por defecto
                y con los teléfonos antiguos.

                hotspot-address es la IP que verán los clientes: 10.10.0.1.
                """,
                code:
                """
                /ip hotspot profile add name=hsprof hotspot-address=10.10.0.1 dns-name=hotspot.mikroadmin login-by=http-chap,http-pap
                /ip hotspot profile print
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: el dns-name es opcional; sirve para que el portal se presente con un nombre. La IP 10.10.0.1 es la que realmente se usa."),
            Step("4.2", "Levanta el servidor hotspot",
                """
                El servidor se aplica a br-client, usa el pool de clientes y el perfil recién
                creado. addresses-per-mac=1 evita que un mismo equipo tome varias direcciones.

                No lo apliques a br-mgmt ni a ether1.
                """,
                code:
                """
                /ip hotspot add name=hs-client interface=br-client address-pool=pool-client profile=hsprof addresses-per-mac=1 disabled=no
                /ip hotspot print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: en cuanto se active, los clientes de GB-Netlan dejarán de navegar hasta que introduzcan un código válido en el portal."),
            Step("4.3", "Excluye la administración de los APs",
                """
                Los APs viven en la red de clientes, pero no son clientes. Agrega un bypass por IP
                para 10.10.0.2 y 10.10.0.3. Así puedes entrar a su configuración desde Mikro-Admin
                sin gastar un ticket; el tráfico de quienes se conectan a su Wi-Fi sigue pasando
                por el portal.

                No hagas bypass de toda la subred 10.10.0.0/24: eso desactivaría el control para
                todos los clientes.
                """,
                code:
                """
                /ip hotspot ip-binding add address=10.10.0.2 type=bypassed comment="EAP110"
                /ip hotspot ip-binding add address=10.10.0.3 type=bypassed comment="TP-Link N"
                /ip hotspot ip-binding print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: el bypass es por dirección de origen. El tráfico de los clientes conserva su IP y su MAC, así que sigue autenticándose."),
            Step("4.4", "Prueba el portal con un usuario temporal",
                """
                Antes de involucrar la app, comprueba que el portal funciona. Crea un usuario de
                prueba con 5 minutos de límite, conéctate a GB-Netlan, abre el portal e inicia
                sesión. Debe navegar y, a los 5 minutos, quedar agotado.

                Al terminar, borra el usuario para no dejar basura.
                """,
                code:
                """
                /ip hotspot user add name=111111 password=111111 limit-uptime=5m comment="prueba"
                /ip hotspot user print
                /ip hotspot user remove [find name="111111"]
                """,
                example:
                """
                Red:      GB-Netlan
                Portal:   http://10.10.0.1
                Usuario:  111111
                Clave:    111111
                Duración: 5 minutos
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: usa 111111 solo para la prueba y bórralo. La app genera códigos de seis dígitos y podría coincidir."),
            Step("4.5", "Haz que aparezca el portal",
                """
                Los teléfonos detectan el portal solos y muestran un aviso como "Iniciar sesión en
                la red". Si no aparece, abre el navegador y visita http://10.10.0.1 o
                http://neverssl.com.

                Usa http, no https, para la primera visita. Una página HTTPS puede fallar antes de
                que el cliente esté autenticado y no muestra el portal.
                """,
                example:
                """
                http://10.10.0.1
                http://neverssl.com
                """),
            Step("4.6", "Entiende qué hace y qué no hace el portal",
                """
                El portal pide usuario y contraseña. La app crea usuarios cuyo nombre y contraseña
                son el mismo código de seis dígitos. El precio y la duración se imprimen en el
                ticket; el router no cobra ni valida pagos.

                El portal por defecto es suficiente para empezar. Si lo personalizas, conserva los
                campos de usuario y contraseña y el formulario de login.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: el cobro y la entrega del ticket son tu responsabilidad; el router solo controla el acceso y el tiempo."),
            Step("4.7", "Revisa sesiones y límites",
                """
                Cada perfil que crea la app usa shared-users=1: un ticket, una sesión. El servidor
                hotspot también limita una dirección por MAC. Si un cliente intenta usar el mismo
                código en dos equipos, el segundo no podrá iniciar sesión o desplazará al primero.

                Puedes ver quién está conectado en /ip hotspot active print o desde el app en Tickets.
                """,
                code:
                """
                /ip hotspot active print
                /ip hotspot user print
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: si un cliente se queda sin batería, la sesión puede tardar en cerrarse. El contador solo avanza mientras el router considera activa la sesión."),
            Step("4.8", "Bloquea la administración desde los clientes",
                """
                Los clientes no deben poder abrir WinBox, SSH ni la API del router. Agrega una
                regla al principio del firewall de entrada que descarte esos puertos cuando el
                tráfico venga de br-client.

                El portal (80 y 443) y el DNS siguen permitidos. No bloquees esos puertos.
                """,
                code:
                """
                /ip firewall filter add chain=input in-interface=br-client protocol=tcp dst-port=8291,22,8728,8729 action=drop place-before=0 comment="Clientes: sin administración"
                /ip firewall filter print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si la regla queda después del accept de LAN, no sirve. Verifica que aparezca al principio con /ip firewall filter print."),
            Step("4.9", "Guarda el respaldo del hotspot",
                """
                El portal ya funciona con un usuario de prueba. Guarda otro backup y un export
                antes de tocar certificados y API.

                Si más adelante el portal deja de aparecer, puedes comparar la configuración con
                este punto de restauración.
                """,
                code:
                """
                /system backup save name=hotspot-ok
                /export file=hotspot-ok
                """),
        ]);

    private static GuideChapter CertificateAndApi() => new(
        "5",
        "Certificado y API-SSL",
        "La app solo acepta un certificado de confianza; con Starlink hay que validarlo por DNS.",
        [
            Step("5.1", "Entiende la exigencia de la app",
                """
                La app se conecta con API-SSL al puerto 8729 y valida el certificado: exige que el
                nombre coincida con el host escrito, que la cadena sea de confianza y que el
                certificado esté vigente. Un certificado autofirmado de RouterOS no sirve.

                Por eso el host de la app debe ser un nombre DNS, no una IP privada, y el
                certificado debe incluir ese nombre en su SAN.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: instalar una CA propia en el teléfono no es una solución fiable: Android no la confía para todas las apps y el chequeo de revocación puede fallar."),
            Step("5.2", "Elige el nombre del router",
                """
                Usa un subdominio de un dominio que controles, por ejemplo
                router.tudominio.com. Ese nombre será el host en la app y el SAN del certificado.

                No uses .local ni una IP. La app compara el certificado contra el texto exacto que
                escribas en el campo Host.
                """,
                example:
                """
                Host en la app: router.tudominio.com
                SAN del certificado: DNS:router.tudominio.com
                """),
            Step("5.3", "Crea el registro DNS público",
                """
                En el panel de tu proveedor de DNS crea un registro A para router que apunte a la
                IP pública que ve Starlink, aunque sea una IP CGNAT. Para la validación DNS-01 lo
                importante es que puedas crear registros TXT en ese dominio.

                No necesitas abrir puertos ni tener IP pública: la validación se hace por DNS.
                """,
                example:
                """
                Tipo: A
                Nombre: router
                Valor: la IP pública que muestra whatismyip
                TTL: 300
                """),
            Step("5.4", "Emite el certificado con validación DNS",
                """
                En tu PC, usa una herramienta que soporte DNS-01. Descarga lego.exe desde su
                página de releases y ejecútalo en una carpeta de trabajo. Con Cloudflare el comando
                es el siguiente; cambia el token, el correo y el dominio. Para otro proveedor,
                cambia --dns por el plugin correspondiente.

                El token solo necesita permiso para editar DNS. No lo guardes en el repositorio ni
                lo compartas.
                """,
                code:
                """
                $env:CLOUDFLARE_DNS_API_TOKEN="tu_token"
                lego --email tu@correo.com --domains router.tudominio.com --dns cloudflare --path . run
                """,
                example:
                """
                .lego/certificates/router.tudominio.com.crt   (cadena completa)
                .lego/certificates/router.tudominio.com.key   (clave privada)
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: también sirve acme.sh. Lo importante es obtener la cadena completa y la clave privada; el método HTTP-01 no funciona detrás de CGNAT."),
            Step("5.5", "Importa el certificado en RouterOS",
                """
                Junta la cadena y la clave en un solo archivo PEM. En PowerShell puedes hacerlo
                así; después sube router.pem a Files en WinBox (arrastrar y soltar) o con /tool fetch.

                Importa el archivo sin contraseña si la clave no está cifrada. Revisa que el
                certificado tenga private-key=yes y que el SAN sea el nombre correcto.
                """,
                code:
                """
                Get-Content .lego\certificates\router.tudominio.com.crt, .lego\certificates\router.tudominio.com.key | Set-Content router.pem

                /certificate import file-name=router.pem passphrase=""
                /certificate print detail
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si RouterOS muestra el certificado sin clave privada, vuelve a importar el PEM completo. Sin clave privada el servicio TLS no arranca."),
            Step("5.6", "Asigna el certificado a api-ssl",
                """
                Activa api-ssl en el puerto 8729 con el certificado importado y desactiva la API
                sin cifrar. La app no usa la API plana.

                Si hay varios certificados, el find por common-name debe devolver el que tiene la
                clave privada.
                """,
                code:
                """
                /ip service set api-ssl certificate=[/certificate find where common-name="router.tudominio.com"] port=8729 disabled=no
                /ip service disable api
                /ip service print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: no desactives WinBox ni SSH antes de comprobar que api-ssl funciona; son tu vía de rescate."),
            Step("5.7", "Resuelve el nombre dentro de la red",
                """
                El certificado es público, pero la conexión es local. Crea una entrada DNS estática
                para que router.tudominio.com resuelva a 192.168.88.1 dentro de tu red, y entrega
                ese DNS a los equipos de administración por DHCP.

                Así el teléfono valida el nombre del certificado y llega al router local, sin
                depender de la IP pública.
                """,
                code:
                """
                /ip dns static add name=router.tudominio.com address=192.168.88.1
                /ip dhcp-server network set [find address=192.168.88.0/24] dns-server=192.168.88.1
                /ip dns static print
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: en Android desactiva DNS privado para esa Wi-Fi si el nombre resuelve a la IP pública. El DNS del router debe ser el que responda."),
            Step("5.8", "Crea el usuario de la API",
                """
                Crea un grupo con los permisos mínimos que usa la app: api, read, write y test.
                Después crea el usuario mikroadmin con una contraseña distinta a la de admin.

                No uses el usuario admin en la app: si algún día cambias su contraseña, la app
                dejará de conectar sin una razón aparente.
                """,
                code:
                """
                /user group add name=mikroadmin policy=api,read,write,test
                /user add name=mikroadmin group=mikroadmin password="ClaveApiLarga#2026" comment="Mikro Admin"
                /user print
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: si una operación falla por permisos, revisa el grupo antes de dar full. La app solo necesita api, read, write y test."),
            Step("5.9", "Comprueba el TLS desde la PC",
                """
                Antes de abrir la app, comprueba que el puerto responde y que el certificado es el
                correcto. Test-NetConnection verifica la conectividad; openssl muestra el nombre,
                el emisor y las fechas.

                Si el nombre no resuelve, revisa el DNS del PC o agrega una entrada hosts temporal.
                """,
                code:
                """
                Test-NetConnection router.tudominio.com -Port 8729

                openssl s_client -connect router.tudominio.com:8729 -servername router.tudominio.com
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: dentro de openssl revisa subject, issuer, dates y subjectAltName. Si el SAN no coincide, la app rechazará la conexión."),
            Step("5.10", "Agenda la renovación",
                """
                Los certificados gratuitos suelen durar 90 días. Si vencen, la app deja de conectar
                aunque el router siga funcionando. Anota la fecha y renueva a los 60 días.

                Para renovar, repite la emisión, sube el nuevo PEM, impórtalo y vuelve a asignarlo
                a api-ssl. No basta con reemplazar el archivo.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: el ACME integrado de RouterOS suele validar por HTTP y no funciona con CGNAT. Usa DNS-01 y una tarea de recordatorio."),
            Step("5.11", "Endurece los servicios del router",
                """
                Limita WinBox, SSH y api-ssl a la red de administración y desactiva lo que no uses.
                No cierres todas las puertas a la vez: deja WinBox por cable como rescate.

                La regla del capítulo 4 ya impide que los clientes del hotspot lleguen a estos
                puertos.
                """,
                code:
                """
                /ip service set winbox address=192.168.88.0/24
                /ip service set ssh address=192.168.88.0/24
                /ip service set api-ssl address=192.168.88.0/24
                /ip service disable telnet,ftp,www
                /ip service print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si te quedas fuera, usa WinBox por MAC desde ether4. No desactives WinBox ni el acceso por cable."),
            Step("5.12", "Guarda el respaldo con certificados",
                """
                El export de texto no incluye la clave privada; el backup binario sí. Guarda los
                dos y protégelos como guardarías la contraseña del router.

                Si restauras el backup en otro equipo, el certificado y la clave viajan con él.
                """,
                code:
                """
                /system backup save name=api-ok
                /export file=api-ok
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: guarda el PEM y la clave también en tu gestor de contraseñas. Si pierdes la clave, deberás emitir otro certificado."),
        ]);

    private static GuideChapter AppAndTickets() => new(
        "6",
        "La app: perfiles y tickets",
        "Conectar Mikro Admin, crear productos y emitir el primer lote.",
        [
            Step("6.1", "Prepara el teléfono o la PC",
                """
                Conéctate a Mikro-Admin, no a GB-Netlan. Comprueba que recibes una IP
                192.168.88.x, que el nombre router.tudominio.com resuelve a 192.168.88.1 y que
                el puerto 8729 responde.

                Si el teléfono tiene DNS privado o una VPN activa, desactívalos para esta red.
                """,
                example:
                """
                Wi-Fi: Mikro-Admin
                IP:    192.168.88.x
                Host:  router.tudominio.com → 192.168.88.1
                Puerto: 8729
                """),
            Step("6.2", "Conecta la app en modo real",
                """
                Abre Mikro Admin, desactiva Simulación y escribe el host, el puerto, el usuario y la
                contraseña de la API. Deja TLS activado: el modo real lo exige.

                Pulsa Conectar. Si todo está bien, la pantalla de Inicio mostrará el router y la app
                empezará a vigilar los tickets cada 30 segundos.
                """,
                example:
                """
                Simulación: desactivada
                Host:       router.tudominio.com
                Puerto:     8729
                Usuario:    mikroadmin
                Contraseña: la del paso 5.8
                TLS:        activado
                """),
            Step("6.3", "Si la conexión falla",
                """
                La app distingue pocos errores, así que prueba en este orden:
                1) ¿Resuelve el nombre? Debe dar 192.168.88.1.
                2) ¿Responde el puerto 8729? Revisa api-ssl y el firewall.
                3) ¿El certificado tiene el SAN correcto y está vigente? Revisa la hora del router.
                4) ¿El usuario tiene api, read, write y test?

                El capítulo 9 tiene la tabla completa de síntomas y causas.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: no cambies cinco cosas a la vez. Arregla una, vuelve a conectar y anota el error exacto que muestra la app."),
            Step("6.4", "Crea los perfiles",
                """
                En Perfiles, crea un perfil por producto. El nombre es el que verás en la app y en
                RouterOS; la velocidad se escribe como descarga/subida; la duración acepta 1h, 12h,
                7d o 01:00:00.

                La app crea el perfil en el hotspot con shared-users=1. No lo renombres desde
                WinBox: los tickets guardan el nombre y dejarían de encontrar su perfil.
                """,
                example:
                """
                Nombre        Velocidad   Duración
                1 hora        3M/3M       1h
                5 horas       4M/4M       5h
                12 horas      5M/5M       12h
                1 día         5M/5M       1d
                3 días        3M/2M       3d
                7 días        2M/1M       7d
                30 días       1M/1M       30d
                Ilimitado     2M/2M       (vacío)
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: los valores son ejemplos. Mide con un test de velocidad; si la descarga y la subida salen invertidas, intercambia los números."),
            Step("6.5", "Genera el primer lote",
                """
                En Generar elige el perfil, la cantidad (1 a 100), la red Wi-Fi y el precio. La red
                debe ser exactamente GB-Netlan para que el QR una el teléfono a esa Wi-Fi.

                La app crea cada ticket con el código de seis dígitos como usuario y contraseña.
                El lote muestra el progreso y se puede cancelar si son muchos tickets.
                """,
                example:
                """
                Perfil:  1 día
                Cantidad: 20
                Red:     GB-Netlan
                Precio:  20.00
                """),
            Step("6.6", "Imprime, comparte o exporta",
                """
                Al terminar tienes cuatro salidas:
                • Imprimir: tarjetas con QR, 30 por hoja carta.
                • PDF: para enviar por mensajería o imprimir después.
                • CSV: para llevar el control en Excel.
                • HTML: una página para compartir.

                Escanea un QR de prueba antes de imprimir cien: debe abrir la Wi-Fi GB-Netlan y
                mostrar el código en la tarjeta.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: si dejas vacía la red Wi-Fi, el QR no podrá unir el teléfono a la red. Escribe GB-Netlan siempre."),
            Step("6.7", "Prueba un ticket de punta a punta",
                """
                Toma un código del lote, conéctate a GB-Netlan desde otro teléfono y escanea el QR.
                Inicia sesión con el código como usuario y contraseña. Debe navegar.

                En la app, Tickets debe mostrar ese código como En línea. Al desconectar el
                teléfono pasará a Pausado, y el contador solo avanzará mientras esté conectado.
                """,
                example:
                """
                Código: 483920
                Usuario: 483920
                Contraseña: 483920
                """),
            Step("6.8", "Gestiona tickets agotados",
                """
                Cuando un ticket consume su tiempo, la app lo muestra como Agotado y lanza una
                notificación local mientras la app está conectada. El aviso te lleva a Tickets.

                Usa Limpiar agotados para borrar esos usuarios del router. Hasta que lo hagas, el
                código sigue existiendo y no se puede reutilizar.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: limpia los agotados al cerrar el día. Libera memoria del hAP lite y evita códigos viejos que confundan al cliente."),
            Step("6.9", "Usa el histórico y el modo simulado",
                """
                Histórico guarda cada lote con fecha, perfil, códigos y precio; desde ahí puedes
                reimprimir o volver a exportar. Es tu respaldo de ventas.

                El modo simulado sirve para practicar sin tocar el router, pero escribe en el mismo
                histórico local. No mezcles pruebas con lotes reales: usa perfiles y precios
                claramente distintos o respalda el archivo antes.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: el histórico vive en el teléfono. Si cambias de equipo o reinstalas la app, exporta antes lo que necesites conservar."),
            Step("6.10", "Buenas prácticas de operación",
                """
                • Un perfil por producto; no cambies la duración esperando que afecte a tickets ya
                  creados: los existentes conservan su tiempo.
                • No borres un perfil que esté en uso: los tickets pierden la velocidad y los
                  ajustes del perfil.
                • Genera lotes pequeños al principio y valida el primero antes de imprimir.
                • Anota qué lote vendiste a quién si manejas varios puntos.
                • Revisa la notificación de agotados y limpia el router con regularidad.
                """),
        ]);

    private static GuideChapter EndUser() => new(
        "7",
        "El cliente final",
        "Del QR al portal, con el código del ticket y el tiempo pausándose al desconectar.",
        [
            Step("7.1", "Entrega el ticket",
                """
                Entrega la tarjeta impresa, una foto clara o el PDF. El cliente necesita ver el
                código de seis dígitos y la red a la que debe conectarse.

                Explícale dos cosas: el código es usuario y contraseña, y el tiempo solo corre
                mientras está conectado.
                """,
                example:
                """
                Red: GB-Netlan
                Código: 483920
                Duración: 1 día
                """),
            Step("7.2", "Conéctate a la red",
                """
                Abre la cámara y escanea el QR de la tarjeta. El teléfono propondrá unirse a
                GB-Netlan, que es una red abierta. Acepta el aviso de red sin seguridad.

                Si el QR no abre la Wi-Fi, entra manualmente a los ajustes de Wi-Fi y conéctate a
                GB-Netlan.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: el QR une a la red cuando el lote se generó con el campo Red Wi-Fi. Si se generó vacío, solo muestra el código."),
            Step("7.3", "Abre el portal",
                """
                Espera unos segundos. Android suele mostrar "Iniciar sesión en la red" y iOS abre
                una hoja de inicio de sesión. Toca el aviso para abrir el portal.

                Si no aparece, abre el navegador y visita http://10.10.0.1 o http://neverssl.com.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si el teléfono tiene datos móviles activos, algunas apps no detectan el portal. Desactiva los datos un momento o abre el navegador a mano."),
            Step("7.4", "Inicia sesión con el código",
                """
                En el portal escribe el código de seis dígitos en Usuario y la misma cosa en
                Contraseña. Acepta las condiciones si el portal las muestra y pulsa el botón de
                inicio de sesión.

                Si el código empieza por cero, escribe los seis dígitos completos.
                """,
                example:
                """
                Usuario:    483920
                Contraseña: 483920
                """),
            Step("7.5", "Verifica el tiempo y la pausa",
                """
                Navega un momento para confirmar que hay internet. El tiempo del ticket se descuenta
                mientras la sesión está activa: si el cliente se desconecta, el contador se pausa y
                puede volver más tarde con el mismo código.

                Cuando el tiempo llegue a cero, el portal rechazará el código y la app lo mostrará
                como Agotado.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: el tiempo es acumulado por sesión conectada; no es tiempo de reloj. Un ticket de 1 hora puede usarse en varias visitas hasta completar 60 minutos."),
            Step("7.6", "Un ticket, un dispositivo",
                """
                El perfil usa shared-users=1 y el servidor limita una dirección por MAC. Si el
                cliente intenta el mismo código en un segundo equipo, no podrá iniciar sesión o
                desplazará la primera sesión.

                Para cambiar de equipo, desconecta el primero o espera a que el router cierre la
                sesión inactiva.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: no prometas varios dispositivos por ticket con la configuración actual. Si lo necesitas, cambia shared-users en el perfil, pero consume más ancho de banda."),
            Step("7.7", "Cuando el ticket se agota",
                """
                El cliente puede comprar otro ticket. Tú generas un lote nuevo o un ticket suelto,
                le entregas el código nuevo y limpias el agotado desde Tickets.

                Los códigos son únicos; no reutilices uno agotado. Si necesitas que el cliente
                conserve el mismo código, no es posible con este flujo: se emite uno nuevo.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: limpia el código agotado después de vender el nuevo, no antes: así no confundes cuál quedó libre."),
            Step("7.8", "Problemas típicos del cliente",
                """
                • No aparece el portal: abre http://10.10.0.1, desactiva VPN y datos móviles.
                • El código no funciona: revisa los seis dígitos, que no esté agotado y que no se
                  esté usando en otro equipo.
                • Se conecta pero no navega: vuelve a iniciar sesión en el portal; la sesión pudo
                  expirar.
                • El teléfono se une a otra red: olvida la red vieja y elige GB-Netlan.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: ten a mano http://10.10.0.1 escrito en la tarjeta. Es la forma más rápida de recuperar el portal."),
        ]);

    private static GuideChapter Checklist() => new(
        "8",
        "Verificación final",
        "El recorrido que debes completar antes de dar la instalación por terminada.",
        [
            Step("8.1", "Checklist físico",
                """
                • Starlink en línea y sin obstrucciones graves.
                • ether1 conectado a Starlink; LED de enlace encendido.
                • ether2 al EAP y ether3 al TP-Link; ambos alimentados.
                • ether4 libre para el PC de administración.
                • Etiquetas en los cables y en los puertos.
                """),
            Step("8.2", "Checklist de red",
                """
                • La WAN recibe IP por DHCP de Starlink.
                • El router hace ping a 1.1.1.1 y resuelve un nombre público.
                • Mikro-Admin entrega 192.168.88.x y navega.
                • GB-Netlan entrega 10.10.0.x.
                • Los APs responden en 10.10.0.2 y 10.10.0.3 desde administración.
                """,
                code:
                """
                /ip dhcp-client print
                /ping 1.1.1.1 count=4
                /ping google.com count=2
                /ip dhcp-server lease print
                /ping 10.10.0.2 count=2
                /ping 10.10.0.3 count=2
                """),
            Step("8.3", "Checklist del hotspot",
                """
                • El portal aparece al conectarse a GB-Netlan.
                • Un usuario de prueba navega y se agota al cumplir su tiempo.
                • Los APs están en bypass y se pueden administrar sin ticket.
                • Un cliente no puede abrir WinBox, SSH ni la API del router.
                • Limpiar agotados borra el usuario del router.
                """,
                code:
                """
                /ip hotspot print
                /ip hotspot active print
                /ip hotspot ip-binding print
                /ip firewall filter print
                """),
            Step("8.4", "Checklist de la app",
                """
                • Conecta en modo real por router.tudominio.com:8729 con TLS.
                • Crea un perfil y genera un lote pequeño.
                • El QR une a GB-Netlan y el código inicia sesión.
                • Tickets muestra En línea, Pausado y Agotado correctamente.
                • La notificación de agotado aparece y abre Tickets.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: prueba el lote con un solo ticket antes de imprimir. Es más rápido corregir un error con un código que con cien."),
            Step("8.5", "Prueba de reinicio",
                """
                Reinicia el hAP lite, el EAP, el TP-Link y el teléfono. La configuración debe
                sobrevivir y los clientes deben volver a conectarse sin reconfigurar nada.

                Si algo no vuelve, revisa que no dependiera de una sesión o de un DHCP temporal.
                """,
                code:
                """
                /system reboot
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: reiniciar no borra la configuración, pero sí corta las sesiones activas. Hazlo cuando no haya clientes usando tickets."),
            Step("8.6", "Cierra con respaldos y contraseñas",
                """
                Guarda un backup y un export finales, descárgalos y anota en tu gestor de
                contraseñas: usuario admin, usuario API, clave de Mikro-Admin, dominio y fecha de
                vencimiento del certificado.

                Deja una copia del PEM y de la clave privada. Sin ellos no podrás restaurar el TLS
                en otro equipo.
                """,
                code:
                """
                /system backup save name=final-ok
                /export file=final-ok
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: programa un recordatorio a 60 días para renovar el certificado y otro para revisar los respaldos."),
        ]);

    private static GuideChapter Troubleshooting() => new(
        "9",
        "Problemas frecuentes",
        "Síntoma, causa probable y qué revisar primero.",
        [
            Step("9.1", "El hAP no tiene internet",
                """
                Revisa en orden: LED de ether1, DHCP client, dirección y ruta por defecto, NAT y
                ping. Si la WAN tiene IP pero no hay ping, el problema está en Starlink; si no hay
                IP, el problema está en el cable o en el modo del router Starlink.

                En bypass, el router Starlink no debe estar enrutando. En doble NAT, el hAP debe
                recibir una IP 192.168.1.x.
                """,
                code:
                """
                /ip dhcp-client print
                /ip address print
                /ip route print
                /ip firewall nat print
                /ping 1.1.1.1 count=4
                """),
            Step("9.2", "La app no conecta",
                """
                Comprueba primero la red: el teléfono debe estar en Mikro-Admin y resolver el
                dominio a 192.168.88.1. Después el puerto 8729, el servicio api-ssl y el firewall.
                Por último, el usuario y sus permisos.

                No pruebes desde GB-Netlan: ese cliente está detrás del portal y, además, el
                firewall le bloquea la administración.
                """,
                code:
                """
                Test-NetConnection router.tudominio.com -Port 8729
                /ip service print
                /ip firewall filter print
                /log print
                """),
            Step("9.3", "Error de certificado",
                """
                Los tres fallos más comunes:
                • El host de la app no coincide con el SAN. Escribe el nombre exacto del certificado.
                • La cadena no es de confianza. Importa el intermedio y revisa que el certificado
                  tenga la clave privada.
                • El certificado venció o el reloj del router está mal.

                Un certificado autofirmado nunca será aceptado por la app.
                """,
                code:
                """
                /certificate print detail
                /system clock print
                /system ntp client print
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: si el router reinició sin NTP y el reloj quedó en 1970, el certificado parecerá inválido aunque sus fechas sean correctas."),
            Step("9.4", "No aparece el portal",
                """
                Confirma que el cliente está en GB-Netlan (10.10.0.x), que el hotspot está activo
                en br-client y que su IP no quedó en un bypass. Abre http://10.10.0.1 a mano.

                Si el cliente está conectado a Mikro-Admin, no verá el portal: esa red es de
                administración y no pasa por el hotspot.
                """,
                code:
                """
                /ip hotspot print
                /ip hotspot ip-binding print
                /ip hotspot active print
                /ip dhcp-server lease print
                """),
            Step("9.5", "El código no funciona",
                """
                Revisa que el código exista en el router, que no esté agotado y que no esté en uso
                en otro equipo. Un error de un solo dígito también lo invalida.

                Si el usuario fue borrado al limpiar agotados, el código ya no existe: hay que
                emitir uno nuevo.
                """,
                code:
                """
                /ip hotspot user print
                /ip hotspot user print detail where name="483920"
                /ip hotspot active print
                """),
            Step("9.6", "La velocidad es baja",
                """
                El hAP lite tiene puertos de 100 Mbps y un CPU modesto: no esperes la velocidad
                máxima de Starlink. Además, el hotspot aplica el rate-limit del perfil.

                Revisa el perfil del ticket, la interferencia en 2.4 GHz y la cantidad de clientes
                conectados. Si necesitas más de ~90 Mbps, el cuello de botella es el hAP lite.
                """,
                code:
                """
                /ip hotspot user profile print
                /interface print stats
                /interface wireless print
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: para varios clientes simultáneos, un router con puertos gigabit y más CPU cambia más la experiencia que subir la potencia del Wi-Fi."),
            Step("9.7", "Los clientes del AP no reciben IP",
                """
                Revisa el cable: debe ir de un puerto LAN del AP a ether2 o ether3, nunca al WAN
                del TP-Link. El AP debe tener el DHCP desactivado y no debe tener VLAN.

                Si el AP responde pero los clientes no, compara su IP y su gateway con el plan
                10.10.0.0/24.
                """,
                code:
                """
                /interface bridge port print
                /ip dhcp-server lease print
                /ping 10.10.0.2 count=2
                /ping 10.10.0.3 count=2
                """),
            Step("9.8", "La hora del router está mal",
                """
                Sin hora correcta, TLS falla. Configura zona horaria, NTP y espera unos segundos.
                Si el router no tiene internet, no podrá sincronizar: primero arregla la WAN.

                También revisa que el firewall no esté bloqueando NTP de salida.
                """,
                code:
                """
                /system clock print
                /system ntp client print
                /system ntp client servers print
                """),
            Step("9.9", "El ticket sigue En línea aunque el cliente se fue",
                """
                El router mantiene la sesión hasta detectar la desconexión. Puede tardar unos
                minutos. Si el cliente ya no está, puedes cerrar la sesión desde Tickets o esperar
                al timeout.

                No borres el usuario para "cerrarlo": perderías el tiempo consumido y el historial
                de ese ticket.
                """,
                code:
                """
                /ip hotspot active print
                """),
            Step("9.10", "Después de reiniciar nada funciona",
                """
                Revisa que el backup no se haya restaurado en una versión distinta de RouterOS y
                que el certificado siga importado. Comprueba puentes, direcciones y DHCP en ese
                orden.

                Si el router arranca pero no responde por IP, entra por MAC desde ether4 y revisa
                la configuración desde WinBox.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: no restaures un backup de otra versión mayor de RouterOS. Puede dejar el equipo en un estado inconsistente."),
        ]);

    private static GuideChapter Annexes() => new(
        "10",
        "Anexos",
        "Resets, diagnóstico, respaldo y un resumen de los datos de ejemplo.",
        [
            Step("10.1", "Resets de fábrica",
                """
                • Starlink: opción de restablecimiento en la app o botón reset del router, según el
                  modelo. No borra la cuenta.
                • hAP lite: botón reset al encender, o /system reset-configuration desde RouterOS.
                • EAP110-Outdoor: botón reset durante unos 10 segundos.
                • TP-Link N: botón reset durante unos 10 segundos.

                Después de cada reset, espera a que el equipo termine de arrancar antes de
                conectarte.
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: el reset del hAP lite borra usuarios y certificados. Ten el backup a mano antes de hacerlo."),
            Step("10.2", "Comandos de diagnóstico",
                """
                Estos comandos muestran el estado completo sin cambiar nada. Úsalos antes de
                mover una configuración que ya funciona.
                """,
                code:
                """
                /interface print
                /interface bridge port print
                /ip address print
                /ip route print
                /ip dhcp-server print
                /ip dhcp-server lease print
                /ip hotspot print
                /ip hotspot active print
                /ip hotspot user print
                /ip firewall filter print
                /ip service print
                /log print
                """),
            Step("10.3", "Respaldo y restauración",
                """
                Guarda siempre las dos formas: backup binario (incluye claves) y export de texto
                (fácil de leer y comparar). Descárgalos del router; no los dejes solo en Files.

                Para restaurar, usa la misma versión de RouterOS. El import de texto no restaura
                claves privadas.
                """,
                code:
                """
                /system backup save name=respaldo
                /export file=respaldo
                /system backup load name=respaldo
                /import file-name=respaldo.rsc
                """,
                noteKind: GuideNoteKind.Warning,
                note: "Aviso: restaurar un backup reinicia el router y corta las sesiones. Hazlo en una ventana de mantenimiento."),
            Step("10.4", "Glosario mínimo",
                """
                • WAN: enlace hacia Starlink.
                • LAN: red interna del router.
                • Bridge: puerto lógico que agrupa interfaces; aquí separa administración y clientes.
                • DHCP: entrega direcciones IP automáticamente.
                • NAT: traduce las IP privadas a la IP de la WAN.
                • Hotspot: portal que exige un usuario antes de dar internet.
                • Perfil: velocidad y duración que hereda un ticket.
                • Ticket: usuario y contraseña de seis dígitos, con tiempo limitado.
                • Uptime: tiempo acumulado con la sesión conectada.
                • CGNAT: IP pública compartida; impide recibir conexiones desde fuera.
                • SAN: nombres que cubre un certificado TLS.
                • API-SSL: servicio cifrado de RouterOS en el puerto 8729.
                """),
            Step("10.5", "Resumen de la instalación",
                """
                Si tuvieras que repetirla de memoria, este es el esqueleto:
                1) Starlink en bypass entrega internet a ether1.
                2) br-mgmt (192.168.88.0/24) para administración; br-client (10.10.0.0/24) para
                   clientes.
                3) EAP 10.10.0.2 y TP-Link 10.10.0.3 en modo AP, sin DHCP, canales 6 y 11.
                4) Hotspot en br-client con pool 10.10.0.100-200 y bypass para los APs.
                5) Certificado DNS-01, api-ssl 8729 y usuario mikroadmin.
                6) App conectada por router.tudominio.com; perfiles y tickets.
                7) Cliente escanea QR, entra a GB-Netlan e inicia sesión con su código.

                Guarda los respaldos y la fecha de vencimiento del certificado. Esa fecha es la
                única que puede tumbar la app sin que nadie toque nada.
                """,
                noteKind: GuideNoteKind.Tip,
                note: "Consejo: imprime este resumen y déjalo junto al router. La próxima vez no empezarás desde cero."),
        ]);

    private static GuideStep Step(
        string number,
        string title,
        string body,
        string? code = null,
        string? example = null,
        GuideNoteKind noteKind = GuideNoteKind.None,
        string? note = null)
        => new(number, title, body, code, example, noteKind, note);
}

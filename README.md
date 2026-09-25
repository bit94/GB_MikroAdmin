# GB.MikroAdmin

Aplicación .NET MAUI con MVVM para administrar un MikroTik por API. Incluye modo simulado (`TikFakeConnection`) para ejecutar la interfaz sin un router y un adaptador `tik4net` centralizado.

## Seguridad

Las credenciales se serializan únicamente en `SecureStorage`, nunca en código ni preferencias planas. El modo real exige API-SSL/TLS (normalmente `8729`). Configure en RouterOS un certificado de confianza y `api-ssl`; no acepte certificados inválidos ni exponga la API normal (`8728`) a Internet. Restrinja el servicio con firewall/VPN y use una cuenta con privilegios mínimos.

Antes de conectar con tik4net, `RouterCertificateValidator` abre un handshake TLS y rechaza de forma cerrada certificados no confiables, revocados, vencidos o cuyo SAN no coincide con el host. Para IPs, el certificado debe incluir esa IP en su SAN. Como defensa adicional, se recomienda usar VPN (WireGuard/IPsec), firewall y permitir exclusivamente la CA corporativa.

## Ejecutar

```powershell
dotnet restore
dotnet build src/GB.MikroAdmin/GB.MikroAdmin.csproj -f net10.0-windows10.0.19041.0
```

Use el interruptor **Modo simulado** para probar altas, bajas, interfaces y usuarios. En modo real el adaptador traduce el alta a `/ip/hotspot/user/add` con `name`, `password`, `profile` y `limit-uptime`, equivalente a:

```routeros
/ip hotspot user add name=usuario1 password=123 profile=limitado limit-uptime=1h
```

Las pruebas de `tests/GB.MikroAdmin.Tests` usan `TikFakeConnection` y no requieren un router real.

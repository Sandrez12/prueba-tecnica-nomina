# Nómina mensual · Prueba técnica

Aplicación ASP.NET Core MVC en .NET 10. El avance se desarrolla y verifica por bloques; el primer bloque solo comprueba arranque y conexión, no incluye el flujo de nómina.

- Sitio: `prueba-tecnica-demo.somee.com`.
- Alcance y aceptación: [requerimientos](docs/REQUERIMIENTOS.md).
- Cálculo: [reglas v1](docs/REGLAS-NOMINA.md).

## Primer arranque

```sh
dotnet run --project src/Nomina.Web -- --urls http://localhost:5086
```

`GET /health` devuelve `200` si la aplicación arranca. `GET /health/db` devuelve `200` si puede abrir una conexión con SQL Server o `503` si falta la configuración o falla la conexión. Este último endpoint no verifica el esquema y no devuelve secretos ni detalles del error.

En local, configurar `ConnectionStrings:Nomina` con ASP.NET Core User Secrets. En IIS, usar una variable de entorno privada `ConnectionStrings__Nomina` en la configuración desplegada. No agregar la cadena a archivos versionados.

```sh
dotnet publish src/Nomina.Web -c Release --no-self-contained -p:UseAppHost=false -o artifacts/publicacion
```

Publicar el **contenido** de `artifacts/publicacion` en la raíz pública del sitio, incluido el `web.config` generado por el SDK. Usar HTTPS antes de habilitar cualquier formulario que reciba credenciales.

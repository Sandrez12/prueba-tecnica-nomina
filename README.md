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

## Aplicar el esquema en Somee

Con `dotnet-ef` versión `10.0.15` instalado, genera un script sin necesitar la contraseña:

```sh
dotnet ef migrations script --idempotent --project src/Nomina.Web -o artifacts/esquema-somee.sql
```

En el panel HTTPS de Somee, abre **MS SQL → PruebaTecDB → Run scripts → From your computer** y selecciona `artifacts/esquema-somee.sql`. No lo subas al File Manager del sitio. El script registra la migración en `__EFMigrationsHistory`; verifica esa tabla desde la consola T-SQL. `/health/db` solo comprueba la conexión y no sustituye este paso.

## Crear el primer administrador

Una vez aplicado el esquema, configurar `BootstrapAdmin__UserName` y `BootstrapAdmin__Password` como variables privadas de IIS junto con `ConnectionStrings__Nomina` en el `web.config` desplegado. La contraseña debe tener al menos 12 caracteres, con mayúscula, minúscula, número y símbolo. No compartir el archivo configurado ni incluirlo en el repositorio.

Al arrancar, Identity crea los roles de la prueba y, si no existe otro administrador, crea la cuenta y guarda el hash de la contraseña. Verifica el inicio de sesión en `/cuenta/ingresar` por HTTPS. Después elimina ambas variables `BootstrapAdmin__*`; deja la conexión SQL. No existe un formulario de autorregistro. En desarrollo, las claves equivalentes en User Secrets son `BootstrapAdmin:UserName` y `BootstrapAdmin:Password`.

Ejecuta `dotnet test` para comprobar que solo administración accede a su ruta y que el formulario rechaza envíos sin token antifalsificación.

## Dar de alta una persona

Inicia sesión como administrador y abre **Gestionar personas → Nueva persona**. Usa datos ficticios, registra una dirección y un teléfono y escribe el salario en pesos enteros (`1500000`, sin separadores). La cuenta y el rol se guardan juntos; el listado administrativo no muestra contraseñas. Esta versión no edita ni elimina personas.

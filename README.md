# Nómina mensual · Prueba técnica

Aplicación ASP.NET Core MVC en .NET 10 para registrar novedades y cerrar nóminas mensuales con reglas simplificadas.

- Sitio: [prueba-tecnica-demo.somee.com](https://prueba-tecnica-demo.somee.com/).
- Alcance y aceptación: [requerimientos](docs/REQUERIMIENTOS.md).
- Cálculo: [reglas v1](docs/REGLAS-NOMINA.md).
- [Arquitectura y publicación](docs/ARQUITECTURA.md) · [Decisiones](docs/DECISIONES.md) · [Uso de IA](docs/USO-DE-IA.md).
- [Guía de sustentación](docs/SUSTENTACION.md) · [Extractos técnicos de IA](docs/ia/EXTRACTOS.md).

## Alcance de la entrega

- Implementado: login privado, alta y edición de personas, borrado o desactivación según referencias, registro de novedades, vista previa, cierre, consulta histórica y exportación CSV de períodos cerrados.
- Pendiente: Kanban, CRUD de roles y permisos configurables, interfaz de asignación de cuadrillas y corrección de novedades. El acceso de jefatura requiere una cuadrilla asignada.
- Verificación: 15 pruebas con `dotnet test`. La validación manual en Somee se realizó con sesión autenticada y datos ficticios, según el reporte de la persona que probó la entrega.

## Horario de la prueba

19 de noviembre de 2026, hora de Colombia (UTC−05:00):

| Hito | Hora |
| --- | --- |
| Primer mensaje sobre los requisitos previos | 11:57:10 |
| Inicio de la prueba: recepción del enunciado en la sesión de trabajo | 12:23:43 |
| Fin del trabajo técnico y de la preparación de la sustentación | 14:26:11 |

La carga a GitHub y el registro de estas horas se hicieron después del cierre técnico, sin modificar la aplicación ni el despliegue.

## Primer arranque

```sh
env ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Nomina.Web -- --urls http://localhost:5086
```

`GET /health` devuelve `200` si la aplicación arranca. `GET /health/db` devuelve `200` si puede abrir una conexión con SQL Server o `503` si falta la configuración o falla la conexión. Este último endpoint no verifica el esquema y no devuelve secretos ni detalles del error.

Para usar SQL Server en local, configura `ConnectionStrings:Nomina` con ASP.NET Core User Secrets, inicia con `ASPNETCORE_ENVIRONMENT=Development` y aplica el esquema a una base de desarrollo. En IIS, usa una variable de entorno privada `ConnectionStrings__Nomina` en la configuración desplegada. No agregues la cadena a archivos versionados.

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

Inicia sesión como administrador y abre **Gestionar personas → Nueva persona**. Usa datos ficticios, registra una dirección y un teléfono y escribe el salario en pesos enteros (`1500000`, sin separadores). La cuenta y el rol se guardan juntos; el listado administrativo no muestra contraseñas. Desde el listado también puedes editar la ficha o retirar a la persona: si tiene registros vinculados, se desactiva en lugar de eliminarla. La cuenta administrativa está protegida. Esta edición no permite restablecer contraseñas.

## Registrar novedades laborales

Desde **Novedades laborales**, selecciona una persona activa, una fecha no futura y un tipo. Cualquier segundo registro para la misma persona y fecha se rechaza, aunque sea de otro tipo. Tampoco se aceptan nuevas novedades de meses cerrados. Jefatura solo ve y registra para integrantes de cuadrillas que lidera; la asignación de cuadrillas y la corrección de novedades aún no tienen pantalla.

| Método y ruta | Resultado |
| --- | --- |
| `GET /api/novedades` | Hasta 100 novedades recientes visibles para la sesión. |
| `GET /api/novedades/token` | Devuelve el nombre y valor del encabezado antifalsificación y establece su cookie. |
| `POST /api/novedades` | Con la cookie de sesión y el token antifalsificación, acepta un JSON como `{"personId":"id-asignado","date":"2026-11-01","kind":2}` y devuelve `201` con el ID. Tipos: `1` laborado, `2` incapacidad, `3` ausencia, `4` adicional. Un duplicado o un período cerrado devuelve `409`. |

## Calcular y cerrar nómina

Como administrador, abre **Nómina mensual**, consulta el mes y revisa los conceptos antes de pulsar **Cerrar nómina del período**. Cerrar guarda una copia definitiva; no significa que se haya hecho el pago. Si cambian los datos de la vista previa, la aplicación pide revisarla de nuevo. Contabilidad solo consulta períodos cerrados; cada trabajador solo ve su línea y total.

| Método y ruta | Resultado |
| --- | --- |
| `GET /api/nomina/{year}/{month}` | Devuelve la vista previa a administración si el mes está abierto, o el histórico según el rol. Devuelve `404` si no hay reporte visible. |
| `POST /api/nomina/cerrar` | Solo administración. Con la sesión y el token de `GET /api/novedades/token`, envía `year`, `month` y `revision` de la vista previa. Devuelve `200` con el histórico o `409` si cambió, está vacío o ya se cerró. |
| `GET /nomina/{year}/{month}/csv` | Administración y contabilidad pueden descargar el CSV detallado de un mes cerrado; `404` si el período no está cerrado. |

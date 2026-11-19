# Plan de tres horas: publicar el núcleo primero

Los tiempos son relativos al inicio real de la prueba. No reiniciar el reloj al empezar a programar. Reservar los últimos 30 minutos para comprobar la entrega y documentar resultados.

| Tiempo | Entrega verificable |
| --- | --- |
| 0:00–0:30 | Commit inicial solo con levantamiento, reglas y plan. Sin código. |
| 0:30–1:15 | Crear solución MVC y modelo de datos; publicar en Somee una compilación temprana y comprobar que la aplicación arranca y ejecuta una consulta a SQL Server. Si hay bloqueo de hosting, anotarlo y avisar por el canal antes de la hora 2. |
| 1:15–1:45 | Iniciar sesión como administración, crear una persona ficticia y registrar novedades. Comprobar que el servidor rechaza un rol no autorizado y la segunda novedad de una misma fecha. |
| 1:45–2:30 | Calcular, mostrar conceptos y cerrar el mes; exponer REST de novedades y nómina. Ejecutar `dotnet test` y volver a publicar. |
| 2:30–2:45 | Recorrer el flujo en la URL pública y comprobar API, rechazo por permisos y que el histórico no cambia al modificar el salario. |
| 2:45–3:00 | Registrar qué funciona y qué quedó pendiente, revisar secretos y conversación exportada, verificar el remoto y preparar la entrega. |

## Qué hay que diseñar antes de programar cada parte

- **Modelo y acceso.** Un proyecto web y otro de pruebas. Opción B: MVC con Bootstrap más REST. Usar ASP.NET Core Identity para cuentas y EF Core para SQL Server. Definir las relaciones entre roles/permisos, personas, contactos, cuadrillas, actividades, novedades y períodos. Las vistas y la API deben aplicar los mismos permisos; para jefatura o trabajador, comprobar también sobre qué persona se actúa. Crear la primera cuenta administrativa sin autorregistro ni contraseña fija en el repositorio.
- **Cálculo y cierre.** Una función de cálculo sin dependencia de SQL, con los casos de las reglas v1. La vista previa no guarda una nómina definitiva. Validar al confirmar que sus datos no cambiaron, y guardar período y líneas históricas en una transacción. El alta o la edición de novedades debe coordinarse con esa misma operación de cierre: verificar el estado solo antes de escribir no basta si dos solicitudes llegan a la vez. Claves únicas para persona/fecha y año/mes como protección adicional.
- **Despliegue.** Preparar cómo aplicar el esquema en SQL Server, publicar el artefacto sin versionar secretos ni archivos generados y verificar la configuración privada en Somee. Antes de habilitar el login público, comprobar que HTTPS funciona. Un HTTP 200 de la página predeterminada no prueba que ASP.NET Core 10 ni la conexión SQL de la aplicación funcionen.

## Orden de recorte

El objetivo es demostrar el núcleo completo, no acumular pantallas. Si el tiempo aprieta, aplazar CSV, Kanban, CRUD de roles y pantallas adicionales antes de sacrificar permisos, cierre inmutable, pruebas o la verificación pública. CSV es el primer extra, y solo si el núcleo ya pasó en Somee y todavía queda margen.

Cada bloque termina con una comprobación y un commit de alcance claro. En la revisión, explicar en una frase qué resuelve el bloque, qué dato cambia y cómo comprobamos que funciona.

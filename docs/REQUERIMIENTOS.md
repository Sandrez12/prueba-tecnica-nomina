# Levantamiento: nómina mensual trazable

El primer objetivo es que administración pueda crear una persona, registrar sus novedades, revisar el cálculo y cerrar el período desde la aplicación publicada en Somee. El cierre debe conservar lo que se calculó aunque cambien los datos de origen.

## Preguntas y supuestos de trabajo

Estas preguntas surgieron durante el levantamiento. No hay respuestas del cliente registradas. La persona que realiza la prueba adoptó los tres supuestos de la última columna como decisiones oficiales para esta entrega, no como respuestas del cliente.

| Pregunta | Respuesta del cliente | Supuesto |
| --- | --- | --- |
| ¿Un día adicional puede coincidir con un día laborado normal? | No registrada | Una novedad por persona y fecha. Un domingo adicional se registra como `Adicional`, no también como `Laborado`. |
| ¿Hasta dónde puede registrar y corregir novedades una jefatura? | No registrada | Solo para integrantes de su cuadrilla y mientras el período esté abierto. Administración puede operar sobre todas las cuadrillas. No se concede acceso global a jefatura si el filtro por cuadrilla aún no está implementado. |
| ¿Cómo se maneja un cambio salarial a mitad de mes o un error descubierto después del cierre? | No registrada | Se usa un solo salario mensual vigente al cerrar, sin prorrateo. Si cambió desde la vista previa, esta debe actualizarse antes de confirmar. No se reabre un período cerrado: el contador documenta y ajusta el error por fuera de esta versión. |

**Otros supuestos:** período por mes calendario; moneda COP; el cierre es por período completo y no confirma que se haya efectuado el pago. Las actividades del tablero no afectan la nómina. Solo se usan datos ficticios.

## Roles y matriz de permisos

La matriz define los permisos deseados, no certifica que ya existan pantallas. La API y las vistas aplican las mismas restricciones. Una funcionalidad aplazada no se deja disponible sin control.

| Acción | Administración | Jefatura | Trabajador | Contabilidad |
| --- | --- | --- | --- | --- |
| Gestionar personas, cuentas y roles | Sí | No | No | No |
| Registrar y corregir novedades de un período abierto | Todas | Su cuadrilla | No | No |
| Calcular vista previa y cerrar nómina | Sí | No | No | No |
| Consultar detalle publicado de nómina cerrada | Todas | No | Solo propio | Todas |
| Exportar período cerrado | Sí | No | No | Sí |
| Consultar actividades y actualizar estado (aplazado) | Todas | Su cuadrilla | Solo asignadas; actualiza las propias | No |

## Priorización R1–R9

| ID | Primer corte | Después o fuera del corte |
| --- | --- | --- |
| R1 | Roles iniciales, permisos y restricciones comprobables. Modelar roles y permisos. | CRUD y edición de permisos configurables: no bloquean el flujo principal. |
| R2 | Alta con cuenta, documento, salario, estado y al menos una dirección y un teléfono; modelar ambos como colecciones. No borrar si hay historial. | CRUD completo de contactos y personas si hay tiempo; preferir desactivación a borrar datos históricos. |
| R3 | Login sin autorregistro, hash de contraseñas, bootstrap privado de administración y permisos en el servidor. | No publicar credenciales demo ni secretos en el repositorio. |
| R4 | Modelar actividad, responsable y los tres estados. | No construir Kanban en el primer corte: no interviene en nómina. |
| R5 | Registrar novedades desde la web y exponer REST; validar fecha, tipo y unicidad. Administración primero; habilitar jefatura solo con control de cuadrilla. | Mejoras de edición masiva después. |
| R6 | Vista previa con detalle, reglas explícitas y pruebas; exponer REST de nómina. | No implementar liquidación legal ni prorrateo. |
| R7 | Cierre único y consulta de lo guardado; preservar salario, reglas, conceptos y totales. | Pantalla de listado histórico y consulta por trabajador si el núcleo está verificado. |
| R8 | Modelar que la exportación se genera desde el período cerrado y su detalle, sin duplicar la nómina. | Primer extra: CSV por persona y total del período para Contabilidad. |
| R9 | Datos ficticios y un caso que permita verificar cálculo, duplicados, permisos y cierre. | Ampliar datos solo si aporta escenarios útiles. |

## Criterios de aceptación

1. **Acceso:** administración inicia sesión sin registro público. Un rol sin permiso no puede cerrar nómina ni invocando directamente la URL o API.
2. **Alta:** administración crea una persona con los datos mínimos y una cuenta; el sistema no devuelve su contraseña ni guarda texto plano.
3. **Novedades:** se registra una novedad por persona y fecha. Un segundo registro, incluso de otro tipo, se rechaza. La validación también existe en base de datos para evitar duplicados concurrentes.
4. **Cálculo:** una persona con salario de $1.500.000, una ausencia, una incapacidad y dos días adicionales obtiene $1.533.333, con cada concepto visible.
5. **Cierre:** un período se cierra una sola vez. El cierre guarda todos los resultados como una unidad. Cambiar después el salario no altera la consulta del período; la edición de novedades de ese mes se rechaza. Si la vista previa quedó desactualizada, se solicita revisión antes de cerrar.
6. **Entrega:** verificar flujo web y respuestas REST en Somee, y ejecutar con `dotnet test` el caso numérico y al menos un caso de acceso denegado.

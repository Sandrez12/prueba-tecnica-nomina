# Sustentación: demostrar el cierre inmutable

En una frase: el sistema registra novedades sin duplicar fechas, explica el cálculo del mes y conserva exactamente los importes que se aprobaron al cerrar. No es una liquidación legal ni un sistema de pagos.

## Demo de 5 minutos

Ensayar con datos ficticios, no con datos personales. Si ya venció el plazo, usa un período cerrado para revisar el histórico y reserva las operaciones de escritura para una copia local de desarrollo.

1. **Personas:** muestra una ficha activa con salario de $1.500.000. Explica que no hay autorregistro y que una persona con registros vinculados se desactiva en lugar de borrarse.
2. **Novedades:** muestra cuatro fechas del mismo mes: una ausencia, una incapacidad y dos adicionales. Explica por qué un segundo tipo para la misma fecha se rechaza.
3. **Nómina:** muestra la fila de ejemplo: `1.500.000 − 50.000 − 16.667 + 100.000 = 1.533.333`. El total del período puede incluir a otras personas.
4. **Histórico y CSV:** compara el salario vigente de la ficha con el salario guardado en un mes cerrado. Descarga el CSV y compara su total con la pantalla. Si quieres mostrar los datos en bruto, abre con sesión `GET /api/nomina/{año}/{mes}`.
5. **Permisos:** muestra cómo `dotnet test` prueba que un trabajador recibe `403` si intenta cerrar desde la web o desde la API.

## Qué saber explicar sin leer un guion

| Si preguntan… | Dónde mirarlo y qué explicar |
| --- | --- |
| ¿Por qué no se duplica una incapacidad? | `WorkEventsController.cs` valida persona/fecha y responde con un mensaje. `NominaDbContext.cs` exige unicidad en SQL aun si llegan dos solicitudes a la vez. |
| ¿Qué pasa si alguien cambia el salario antes del cierre? | `PayrollCalculator.Revision` cambia con los datos de entrada. `PayrollService.Close` recalcula dentro de una transacción y pide revisar si la revisión ya no coincide. No es una contraseña ni un permiso. |
| ¿Qué pasa si lo cambia después? | El histórico y el CSV leen `PayrollPeriod` y `PayrollLine`. No consultan el salario vigente para recalcular. No afirmes que un administrador directo de SQL sea incapaz de editar la base: la aplicación no ofrece esa operación. |
| ¿Por qué MVC con EF Core? | Una publicación en IIS para vistas y API; EF Core permite generar el esquema con claves e índices y aplicar migraciones de forma explícita. El cálculo no depende de EF Core para sus pruebas unitarias. |
| ¿Qué falta o qué mejorarías primero? | Kanban, permisos configurables y pantallas de cuadrillas/correcciones quedaron fuera. Para operación real, prioriza copias automáticas, una restauración probada y auditoría. |

## Cambio en vivo para practicar

En una copia local, simula que una incapacidad ahora debe pagarse al **50 %**:

1. Antes de tocar el código, calcula el nuevo resultado del ejemplo: **$1.525.000 COP**.
2. Escribe primero una prueba para la nueva regla. Localiza el divisor, los parámetros persistidos y la versión en `src/Nomina.Web/Payroll/PayrollCalculator.cs`.
3. Cambia la versión para los cierres nuevos y actualiza la documentación. Ejecuta `dotnet test`. Explica por qué un histórico ya guardado sigue mostrando las reglas v1 y no necesita recalcularse.

Este es un ejercicio de preparación, no un cambio aplicado a la entrega ni a Somee.

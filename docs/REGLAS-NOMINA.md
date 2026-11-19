# Reglas de nómina v1

Reglas simplificadas para la prueba. No son una liquidación legal colombiana. Los ajustes de ley quedan a cargo del contador fuera de la aplicación.

## Cálculo por persona

El mes es calendario, pero la **tarifa diaria siempre es salario mensual ÷ 30**, incluso en febrero o en un mes de 31 días. No se prorratea por cambio salarial, alta o baja a mitad de mes. Solo se incluyen personas activas al corte; la cuenta técnica de administración no participa en la liquidación. Se permite cerrar el mes actual, pero no uno futuro; el cierre no indica que se efectuó el pago.

| Concepto | Importe |
| --- | --- |
| Salario base mensual | Salario utilizado para el período. |
| Laborado normal | $0 de ajuste: ya está en el salario mensual. |
| Ausencia | Resta una tarifa diaria por cada día. |
| Incapacidad | Se paga a dos tercios de la tarifa diaria; como el salario ya incluye el día completo, se resta un tercio por cada día. |
| Adicional | Suma una tarifa diaria por cada día, sin recargos legales. |

Calcular con `decimal`, sin redondear primero la tarifa diaria. Redondear el importe agregado de cada concepto al peso más cercano (los medios pesos se alejan de cero) y sumar los importes mostrados. Si el resultado es negativo, mostrar un ajuste explícito a cero. El total del período es la suma de los totales por persona.

## Ejemplo completo

Salario **$1.500.000 COP**; tarifa diaria **$50.000**. Una ausencia, una incapacidad y dos adicionales, en cuatro fechas distintas:

| Concepto | Cálculo | COP |
| --- | --- | ---: |
| Salario | Base mensual | 1.500.000 |
| Ausencia | −1 × 50.000 | −50.000 |
| Incapacidad | −1 × 50.000 ÷ 3, redondeado | −16.667 |
| Adicionales | 2 × 50.000 | +100.000 |
| **Total** | Suma de los conceptos | **1.533.333** |

## Casos borde

- **Mes sin novedades:** se paga el salario base; una fecha sin registro no se interpreta como ausencia. Esto no prueba asistencia.
- **Fechas y duplicados:** no aceptar fechas futuras ni fechas inválidas o ajenas al período seleccionado. Una persona tiene como máximo un tipo por fecha; corregir implica editar el registro, no insertar otro.
- **Salario y personas:** rechazar salarios negativos; permitir cero para datos de prueba. Si una persona está inactiva al corte, no entra en un nuevo cálculo, pero conserva sus resultados previos.
- **Vista previa desactualizada:** si cambian salario, novedades o participantes antes del cierre, la vista previa se debe actualizar y revisar. No se confirma silenciosamente otro resultado.
- **Histórico:** cerrar guarda el período, cuándo y quién lo cerró, la versión y los parámetros de las reglas y, por persona, el salario, las cantidades, los importes y el total. Las consultas y exportaciones históricas leen los importes guardados, no recalculan con datos actuales.
- **Cierre repetido o edición posterior:** rechazar el segundo cierre y los cambios de novedades del período cerrado. No hay reapertura ni eliminación del resultado por la aplicación.

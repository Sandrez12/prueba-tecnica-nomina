# Extractos técnicos de la conversación de IA

Selección de intercambios reales de OpenCode para esta prueba. Las citas son fragmentos, no una exportación íntegra. Se omitieron mensajes de configuración personal, capturas y credenciales. El trabajo también incluyó asistencia de implementación y pruebas, descrita en [Uso de IA](../USO-DE-IA.md).

## Definir los supuestos del cliente

> **Usuario:** «dime que consideras tu para las 3 pregntas que me haces»

> **Asistente:** «**Una novedad por persona y fecha.** Si alguien trabaja un domingo adicional, se registra como `Adicional`, no también como `Laborado`.»

> **Asistente:** «**Cada jefe registra y corrige novedades solo de su cuadrilla y antes del cierre.** Administración puede operar sobre todas.»

> **Asistente:** «**Un solo salario para el mes: el vigente al cerrar, sin prorrateo.** Si cambia después de calcular la vista previa, hay que revisarla de nuevo antes de cerrar.»

> **Usuario:** «me gustan estos 3 supuestos, trabajemos con ellos como oficiales»

## Elegir arquitectura

> **Usuario:** «antes de seguir, porque debe ser mvc?»

> **Asistente:** «**No tiene que ser MVC.** Lo obligatorio es **ASP.NET Core con .NET 10 y SQL Server**. MVC es la arquitectura que propuse para la **opción B**; también puedes elegir la **opción A** y usar React, Vue u otro framework que consuma la API.»

> **Asistente:** «La vista Razor muestra el resultado como tabla; si llamas a la API, recibes JSON. **La fórmula vive en el servicio, no en la vista ni duplicada en la API.**»

En la implementación final, el servicio llama a `PayrollCalculator`, donde están las fórmulas.

## Entender la verificación en IIS

> **Usuario:** «no veo la carpeta /health»

> **Asistente:** «No vas a ver una carpeta `health` en el File Manager. Es una **ruta HTTP** definida en `Program.cs`: cuando abres esa URL, ASP.NET Core ejecuta código y devuelve JSON, en vez de buscar una carpeta o un archivo.»

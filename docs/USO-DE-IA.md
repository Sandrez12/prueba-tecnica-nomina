# Uso de IA

Usé OpenCode como asistente de implementación, revisión y diagnóstico. Consulté documentación con Context7 y contrasté resultados con el SDK de .NET, Git, las pruebas y las respuestas del sitio.

| Momento | Qué corregimos o verificamos |
| --- | --- |
| VS Code | La primera orden usó `./` pero el paquete estaba en `Downloads`. Reproduje el error y confirmé el arreglo simulando la instalación con la ruta absoluta. |
| Node.js | El procedimiento inicial no distinguía Fish de Bash. El script se cargó correctamente en Bash; Fish reprodujo el error. Verifiqué que Node reportaba un nombre de LTS. |
| Autorización | La primera prueba con roles simulados devolvió `302` tanto para un trabajador como para administración. Configuré explícitamente el esquema de autenticación de prueba y confirmé `403` y `200`, respectivamente. |
| Actualización en Somee | La segunda publicación falló al sobrescribir un archivo. Verifiqué que el ZIP era válido y que el login seguía dando `404`; la publicación se repitió con el sitio detenido y la persona que probó confirmó que pudo iniciar sesión. El mensaje de Somee no identificó la causa exacta. |

El asistente ayudó con código y pruebas, pero no recibió contraseñas para configurar el sitio. La aprobación de los supuestos, la operación del panel y la verificación con sesión autenticada quedaron a cargo de la persona que entrega. Para el cierre contrasté el ejemplo documentado con una prueba automatizada y verifiqué los rechazos de acceso tanto en vistas como en API.

La evidencia adjunta es una [selección de extractos reales](ia/EXTRACTOS.md), no una exportación íntegra. Se excluyeron capturas y datos privados, incluida una captura histórica de configuración cuya contraseña SQL ya fue rotada.

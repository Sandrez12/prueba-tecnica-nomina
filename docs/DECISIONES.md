# Decisiones técnicas

| Elección | Por qué | Alternativa descartada |
| --- | --- | --- |
| Opción B: ASP.NET Core MVC con vistas Razor y Bootstrap; REST para novedades y nómina | Formularios y tablas en la misma aplicación y un solo artefacto para IIS. La lógica y permisos no se duplican entre vistas y API. Bootstrap (MIT) aportará estilos y componentes básicos. | SPA separada: añade integración y otro paso de compilación sin aportar al flujo principal. |
| EF Core 10 con proveedor SQL Server (MIT) | Permite definir relaciones, restricciones y migraciones a partir del modelo; el proveedor apunta al mismo motor que Somee. | Dapper: útil para controlar el SQL directamente, pero exigiría mantener más mapeo y scripts de esquema manualmente. |
| Primera publicación dependiente del runtime | Artefacto más pequeño y comprobación directa de que el runtime del servidor ejecuta `net10.0`. | Autocontenida: incluye el runtime y ocupa más espacio; no es necesaria si la publicación inicial pasa. |

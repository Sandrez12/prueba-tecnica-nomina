# Arquitectura y despliegue

```text
Navegador ── HTTPS ──> IIS en Somee
                         └─ ASP.NET Core MVC .NET 10
                             ├─ Vistas Razor / Bootstrap y API REST
                             ├─ Identity: sesión y roles
                             └─ EF Core ──> SQL Server en Somee
```

Una sola aplicación sirve la interfaz y la API. SQL Server impone unicidad por persona/fecha y por año/mes. El cierre guarda el período y sus líneas en una transacción serializable; si los datos de la vista previa cambiaron, pide volver a revisarla. El histórico y el CSV leen las líneas guardadas.

## Publicar y verificar

1. Publicar en Release, sin empaquetar el runtime, según el [README](../README.md). En la primera publicación se incluye el `web.config` de IIS; en actualizaciones se excluye para no sobrescribir la configuración privada.
2. Aplicar el script idempotente de EF Core desde **MS SQL → Run scripts**. Verificar `__EFMigrationsHistory`. No ejecutar migraciones automáticamente al iniciar.
3. Detener el sitio antes de sobrescribir los binarios y volver a iniciarlo al terminar. Si falla la extracción, conservar el `web.config` y restaurar el último paquete que funcionaba, siempre que la migración aplicada sea compatible.
4. Verificar HTTPS con `/health` y SQL con `/health/db`. Con sesión, comprobar el cierre y consultar el período por API. Independientemente, verificar que administración o contabilidad pueden descargar el CSV y que un trabajador no puede.

**Configuración:** cadena SQL en el `web.config` del servidor, fuera de Git. Los valores privados de creación del primer administrador se retiran después de usarlo. No publicar credenciales demo en el repositorio. No se altera la publicidad de Somee.

## Si se amplía a 150 personas

Primero migraría SQL a un servicio con copias automáticas y restauración a un punto en el tiempo. Probaría una restauración completa y fijaría con contabilidad cuánto dato es aceptable perder y cuánto puede durar una interrupción. Después mediría la carga real, añadiría alertas de fallos de cierre y conservaría auditoría de cambios. Escalar la aplicación sin probar primero la recuperación no responde al riesgo principal.

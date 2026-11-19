# Criterios de publicación de la versión posterior

La versión evaluada es el commit `11016b2`. No reemplazarla retroactivamente en la entrega ni publicar desde la rama de trabajo mientras falten verificaciones.

## Cambios en la rama de trabajo

- **Cuadrillas:** administración asigna jefatura e integrantes. Las personas de otra cuadrilla no se trasladan de forma implícita.
- **Novedades:** administración y jefatura en su alcance pueden corregir el tipo de una novedad abierta; se registra quién, cuándo, el motivo y los tipos anterior y nuevo. La persona y fecha conservan su identidad. La API usa `PATCH /api/novedades/{id}` con la cookie de sesión y token de antifalsificación; acepta `previousKind`, `kind` y `reason`.
- **Trabajadores:** pueden consultar una estimación propia provisional de un mes abierto, sin permiso para cerrarlo. Contabilidad sigue limitada a meses cerrados.
- **Contactos y actividades:** edición de múltiples teléfonos y direcciones; tablero de tres estados con responsables y acceso por ámbito.
- **Roles:** administración puede crear, listar, editar y eliminar roles personalizados sin asignaciones. Los roles base conservan su nombre y los permisos operativos iniciales hasta que administración los configure. El rol de Administración está protegido; no se delegan la gestión de cuentas, cuadrillas y roles ni el cierre de nómina. Exportar requiere consulta global de períodos cerrados.

## Antes de publicar

1. Ejecutar la prueba de integración de publicación en SQL Server aislado. Crea una base desechable desde la migración inicial, registra una nómina histórica ficticia, aplica la nueva migración y compara JSON y CSV antes y después de cambiar un salario. Comprueba accesos con sesiones reales, correcciones, CSV y revocación de permisos.
2. Validar el script idempotente y la reversión de la aplicación en una copia de ensayo. Conservar una copia de seguridad y el binario anterior. Si se usa una copia del sitio, sanitizar sus datos y no reutilizar credenciales de producción.
3. Solo después de verificar lo anterior, promover la versión al repositorio y publicarla como actualización posterior a la entrega.

## Ensayo realizado

La prueba `SqlReleaseTests` pasó en SQL Server 2025 Developer aislado, en una base ficticia creada con la migración inicial. Aplicó el script idempotente dos veces, comparó el JSON y el CSV del período cerrado antes y después de migrar y cambiar el salario, y comprobó con sesiones reales el alcance de jefatura, trabajador, contabilidad y un rol personalizado, incluida la revocación de permisos. No se usaron datos ni credenciales de Somee.

## Actualizar Somee

1. Conserva un respaldo de la base y el paquete anterior. En el panel SQL de Somee, ejecuta `artifacts/esquema-v2.sql` mediante **Run scripts**. Comprueba que `dbo.__EFMigrationsHistory` incluya `20261119201818_AuditWorkEventCorrections`.
2. Detén el sitio, extrae `artifacts/nomina-v2.zip` sobre la raíz con sobrescritura y vuelve a iniciarlo. El paquete excluye `web.config`; conserva el existente.
3. Comprueba `/health/db`, inicia sesión como administrador, abre **Roles**, **Cuadrillas** y **Actividades**, y verifica un período cerrado y su CSV. Si falla el arranque, reinstala el paquete anterior conservando `web.config`; la nueva migración añade una tabla y no requiere borrarla para volver al binario anterior.

### Ejecutar la prueba de integración

Configura `NOMINA_TEST_SQL` con una cadena de conexión privada a un SQL Server de ensayo en `127.0.0.1,PUERTO`, con permiso de crear bases. La prueba no admite un servidor remoto y crea una base `NominaRelease_*` con datos ficticios; retírala desde la instancia de ensayo tras la validación. No guardes la cadena en Git.

```sh
dotnet test --filter FullyQualifiedName~SqlReleaseTests
```

Sin `NOMINA_TEST_SQL`, xUnit marca el ensayo como omitido: una ejecución omitida no valida la publicación.

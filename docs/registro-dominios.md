# Registro de dominios aprobados

Fecha: 2026-10-06  
Estado: implementación local verificada; autoridades y almacenamiento de producción pendientes.

## Objetivo

Impedir que un paquete sea aceptado solamente porque contiene `status: approved` y `approvedBy`. La admisión exige una publicación firmada por una autoridad autorizada para ese dominio y un contenido idéntico al publicado.

La implementación está en [ApprovedDomainRegistry.cs](../src/SoftwareAssembly.Core/ApprovedDomainRegistry.cs). Utiliza RSA-PSS con SHA-256 de .NET; no implementa algoritmos criptográficos propios.

## Fronteras de confianza

| Elemento | Responsable y restricción |
|---|---|
| Paquete de dominio | Puede ser elaborado con ayuda de IA; no puede autorizar su propia aprobación. |
| Declaración de aprobación | Debe identificar dominio, versión, digest y aprobador del contenido revisado. |
| Clave privada | Control exclusivo del aprobador o servicio de firma autorizado; fuera del repositorio y contexto del agente. |
| Autoridades públicas | Configuración confiable inyectada por el operador; nunca leída del paquete que se evalúa. |
| Registro | Escritura reservada al publicador; el implementador requiere acceso de lectura, no de edición. |
| Pins de ejecución | Deben conservarse en evidencia protegida para comprobar lecturas posteriores. |

La firma demuestra posesión de una clave autorizada. No verifica por sí sola que una persona haya revisado el negocio ni enlaza automáticamente la clave a un proveedor de identidad.

## Protocolo de aprobación

1. Validar el esquema, los contextos y las fuentes de las reglas del paquete.
2. El responsable revisa el contenido exacto y declara su ID de aprobador.
3. Calcular el digest usando `ConfigurationResolver.Digest`.
4. Construir `DomainApprovalStatement` con dominio, versión, digest e ID del aprobador.
5. Obtener los bytes de `ApprovedDomainRegistry.GetSigningPayload` y firmarlos mediante RSA-PSS/SHA-256 fuera del entorno del implementador.
6. Entregar al publicador el paquete y `SignedDomainApproval`, que contiene la declaración y firma Base64.
7. El publicador valida la autoridad y firma y usa `Publish` para crear una nueva referencia, sin reemplazar una existente.
8. Conservar el pin retornado y configurar la aplicación para seleccionar la versión publicada.

El propósito del protocolo se incluye en los bytes firmados para separar esta aprobación de otros tipos de mensajes. No debe firmarse otro JSON, una representación visual o un digest con una serialización distinta.

Los IDs de aprobador se comparan exactamente y son independientes del vocabulario del negocio. La lista de autoridades define explícitamente qué dominios puede aprobar cada ID.

## Composición del resolutor

Ejemplo ilustrativo con variables obtenidas por el host desde configuración confiable:

```csharp
var validator = new ConfigurationResolver(schemaText);
var registry = new ApprovedDomainRegistry(validator, registryDirectory, trustedAuthorities);
var publishedDomain = registry.Get(domainId, domainVersion);
var resolver = new ConfigurationResolver(schemaText, registry);
var configuration = resolver.Resolve(application, [publishedDomain], profiles, policies);
var pinnedDomain = registry.Get(configuration.Domain);
```

El verificador es obligatorio para `Resolve`. Un resolutor sin verificador puede validar estructura, pero rechaza la admisión con `DOMAIN_APPROVAL_REQUIRED`.

No se ejecuta un proceso de firma automático al resolver. Las firmas se obtienen previamente y el contenido aprobado se verifica de nuevo cada vez que se consulta el registro.

## Publicación y lectura

- Los nombres de archivo se derivan de un hash del ID y versión, no de rutas proporcionadas por el paquete.
- La creación exclusiva impide sobrescribir referencias mediante la API, incluso con otra firma válida.
- Se serializa una copia del paquete antes de escribir; cambios posteriores del objeto del llamador no modifican la publicación.
- Se conservan paquete y declaración firmada, nunca la clave privada.
- Las lecturas verifican esquema, referencias internas, autoridad, firma, digest e identidad de la referencia.
- La lectura con `ConfigurationPin` también verifica que el digest coincida con el esperado por la ejecución.
- Retirar una autoridad de la configuración del registro causa el rechazo de sus firmas al crear nuevas instancias del verificador. No existe actualización dinámica de autoridades en una instancia en curso.

Una nueva regla o modificación del paquete exige una nueva versión y aprobación. No se usa la edición de una versión existente como mecanismo de actualización.

## Pruebas ejecutables

```powershell
dotnet test tests/SoftwareAssembly.Core.Tests/SoftwareAssembly.Core.Tests.csproj --configuration Release --filter "FullyQualifiedName~ApprovedDomainRegistryTests|FullyQualifiedName~ConfigurationResolverTests"
```

La suite cubre publicación válida de dos dominios, ausencia de publicación, autoridades ajenas, claves incorrectas, firmas inválidas, cambios de paquete y metadatos, nuevas versiones, concurrencia, corrupción del almacenamiento y discrepancia de pins.

Las claves privadas de las pruebas se generan en memoria y se destruyen al finalizar. Los registros temporales se eliminan. Estas pruebas no constituyen aprobaciones humanas de los dominios de ejemplo.

## Límites y tareas pendientes

- Configurar autoridades de negocio reales y asociar sus claves a identidades verificadas.
- Implementar el flujo de revisión y firma y su registro de auditoría, sin exponer claves al agente.
- Proteger el directorio o sustituirlo por un backend con controles de acceso e inmutabilidad.
- Definir rotación de claves, revocación operativa y recuperación administrativa de publicaciones parciales.
- Integrar el registro con un host o CLI autorizado; actualmente es una API de biblioteca.

Una interrupción durante la escritura puede dejar una publicación parcial. Se rechaza al leer y no se sobrescribe automáticamente; su recuperación requiere intervención administrativa. El registro no garantiza conservación contra borrado ni sustitución externa de una publicación por otra válidamente firmada si el almacenamiento carece de protección. Una ejecución con pin protegido detecta el cambio de digest.

No se han creado identidades cloud, claves de producción, permisos del sistema ni servicios remotos en este incremento.
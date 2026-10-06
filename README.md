# Software Assembly

Plataforma de ensamblaje de software con IA y dominios configurables. Motor en .NET y perfil inicial para aplicaciones .NET + Angular.

Repositorio: [hon021/software-assembly](https://github.com/hon021/software-assembly).

El directorio local de referencias, las herramientas portables y los artefactos temporales están excluidos de Git. No forman parte del producto publicado.

## Estado

Fase 1 en curso. La base implementada valida configuraciones versionadas, resuelve dominios publicados con firmas verificadas, planifica etapas backend/frontend y comprueba transiciones estructurales. El registro local rechaza sobrescrituras de versiones y lecturas con contenido alterado. Todavía no genera aplicaciones, ejecuta comandos de perfiles, crea PRs ni se conecta a servicios cloud.

## Validación local

Requiere SDK .NET 10 y acceso a NuGet para la primera restauración. No requiere Node.js ni cargar el directorio de referencias.

```powershell
dotnet test SoftwareAssembly.slnx --configuration Release
```

Las pruebas validan los ejemplos de comercio y logística usando el mismo perfil. Incluyen casos negativos para selección ausente, versiones desconocidas, autoridades no autorizadas, firmas inválidas, contextos y fuentes inválidos, publicación concurrente y cambios de contenido.

## Estructura

- [src/SoftwareAssembly.Core/SoftwareAssembly.Core.csproj](src/SoftwareAssembly.Core/SoftwareAssembly.Core.csproj): núcleo genérico .NET.
- [tests/SoftwareAssembly.Core.Tests/SoftwareAssembly.Core.Tests.csproj](tests/SoftwareAssembly.Core.Tests/SoftwareAssembly.Core.Tests.csproj): pruebas del núcleo y de configuración.
- [schemas/configuration.schema.json](schemas/configuration.schema.json): contratos JSON Schema 2020-12 para aplicación, dominio, perfil, política y resultado de gate.
- [applications/tienda/application.json](applications/tienda/application.json) y [applications/distribucion/application.json](applications/distribucion/application.json): aplicaciones sintéticas.
- [domains/comercio/1.0.0/domain.json](domains/comercio/1.0.0/domain.json) y [domains/logistica/1.0.0/domain.json](domains/logistica/1.0.0/domain.json): dominios sintéticos.
- [profiles/dotnet-angular/2.0.2/profile.json](profiles/dotnet-angular/2.0.2/profile.json): contrato backend/frontend con herramientas fijadas y override de seguridad; validación local conforme.
- [profiles/dotnet-angular/1.0.0/profile.json](profiles/dotnet-angular/1.0.0/profile.json): versión histórica descriptiva, conservada para auditoría; no admitida para ejecución.
- [policies/entrega-estandar/1.0.0/policy.json](policies/entrega-estandar/1.0.0/policy.json): restricciones iniciales de entrega.
- [.github/workflows/ci.yml](.github/workflows/ci.yml): CI del motor activo en GitHub Actions, con primera ejecución aprobada; protección de rama pendiente.
- [azure-pipelines.yml](azure-pipelines.yml): definición alternativa no activa.

## Incorporar un dominio

1. Crear un paquete JSON con identificador, versión, responsable, glosario, contextos, fuentes y reglas, conforme al esquema.
2. Validar las referencias de reglas y obtener aprobación del responsable de negocio antes de usar un paquete real.
3. Firmar la declaración de aprobación con la clave del responsable, fuera del entorno del agente, y publicar el paquete en el registro configurado.
4. Configurar una aplicación con referencias explícitas a dominio, perfil y política.
5. Proporcionar los documentos y el verificador del registro al resolutor, y conservar las versiones y digests devueltos.

El resolutor recibe documentos explícitos: no busca información en otros directorios ni incorpora automáticamente otro dominio. Devuelve IDs de reglas aplicables y pins; el ensamblaje del contexto completo para workers se implementará en la fase 2.

Ambas aplicaciones de ejemplo seleccionan el perfil técnico `2.0.2`. El planificador comprueba esquema, rutas declaradas, dependencias, etapas obligatorias, overrides y evidencia. La comparación de versiones observadas está implementada, pero la detección automática y ejecución aislada corresponden al futuro runner. Se ejecutó un smoke test real con herramientas portables locales; no equivale a tener un runner de producción. Véase [Perfil técnico](docs/perfil-tecnico.md).

La versión actual superó la auditoría frontend sin vulnerabilidades conocidas reportadas y las pruebas locales: 99 del motor, 2 unitarias Angular y 1 Chromium. No se redujo el gate de seguridad. Esto no certifica producción ni sustituye CI remoto o aceptación humana. Los resultados e historial están en [Validación local](docs/validacion-local-perfil.md).

Los ejemplos declaran aprobación ficticia exclusivamente para pruebas. Las pruebas generan claves efímeras y publicaciones temporales; no hay claves privadas ni aprobaciones de producción en el repositorio. El campo `approvedBy` por sí solo no permite resolver una aplicación.

La firma demuestra posesión de la clave correspondiente a una autoridad configurada para el dominio. Asociar esa clave a una persona o identidad corporativa, proteger el registro y operar la revisión humana requiere configuración e integración posterior. Véase [Registro de dominios](docs/registro-dominios.md).

## Documentación

- [Propuesta](docs/propuesta-ensambladora-software.md).
- [Fases de implementación](docs/fases-implementacion.md).
- [Arquitectura inicial y límites](docs/arquitectura-inicial.md).
- [Registro de dominios aprobados](docs/registro-dominios.md).
- [Perfil técnico backend/frontend](docs/perfil-tecnico.md).
- [Validación local del perfil y bloqueos](docs/validacion-local-perfil.md).
- [Cierre de fase 1 y pendientes externos](docs/cierre-fase-1.md).
- [CI en GitHub Actions](docs/github-actions.md).
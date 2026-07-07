# Análisis General - Portal DIGER Trámites Estado

Este documento sirve como referencia arquitectónica y funcional del sistema, diseñado para proporcionar un entendimiento completo del estado actual de la plataforma, independientemente del contexto de la conversación.

## 1. Arquitectura y Stack Tecnológico

El proyecto es una aplicación web empresarial moderna y bien estructurada que prioriza la mantenibilidad y la separación de responsabilidades:

*   **Framework Base:** .NET 9 utilizando Razor Pages. La elección de Razor Pages (Server-Side Rendering) es ideal para portales administrativos internos: reduce la complejidad en el cliente, facilita el SEO interno, simplifica la autenticación y reduce el tiempo de desarrollo inicial en comparación con una SPA (Single Page Application).
*   **Patrón Arquitectónico:** **Clean Architecture**. El código está claramente dividido en capas concéntricas (`Domain`, `Application`, `Infrastructure`, `Web`), asegurando que las reglas de negocio (Dominio) no dependan de frameworks externos o bases de datos.
*   **Patrón de Diseño Principal:** **CQRS** (Command Query Responsibility Segregation) implementado mediante **MediatR**. Esto separa las operaciones de lectura (Queries) de las de escritura (Commands), permitiendo escalar y optimizar cada flujo por separado, y manteniendo los controladores (o PageModels en este caso) delgados.
*   **Persistencia:** SQL Server gestionado a través de Entity Framework Core (EF Core).
*   **Seguridad:** Autenticación basada en Cookies estándar de ASP.NET Core con hashing de contraseñas robusto (PBKDF2-SHA256).

## 2. Descripción de Módulos Core

1.  **Expedientes de Digitalización:** Es el corazón operativo. Permite modelar el proceso de digitalización de un trámite gubernamental a través de un asistente (wizard) de 7 pasos. Administra desde la ficha técnica y requisitos legales, hasta el "As-Is" (situación actual) y el "To-Be" (modelo propuesto). Todo fue migrado exitosamente de un modelo JSON crudo a un esquema relacional estructurado.
2.  **Reuniones y Asistencias:** Gestiona actas y compromisos. Destaca por su capacidad de imprimir en PDF de forma nativa (`window.print`) y por alimentar dinámicamente un "Directorio de Contactos" a medida que se ingresan asistentes.
3.  **Tickets de Soporte:** Un módulo integrado de "Help Desk" (Mesa de Ayuda) para atender incidencias de los usuarios. Posee un ciclo de vida claro (Abierto → En Progreso → Resuelto → Cerrado) y permite asignaciones directas.
4.  **Tableros (Dashboards):** Panel analítico rico visualmente, implementado usando CSS nativo y `Chart.js` (alojado localmente para independencia de red). Muestra KPIs en tiempo real de tickets, expedientes y reuniones.
5.  **Usuarios e Instituciones (Multi-tenant ligero):** Gestión de roles (Admin, Coordinador, Técnico) e instituciones.

## 3. Puntos Fuertes del Sistema (Fortalezas Técnicas)

*   **Filtro Global de Autorización por Fila (Row-Level Security):** Es, arquitectónicamente, la característica más robusta. Utiliza **EF Core Global Query Filters** para asegurar que un usuario Coordinador/Técnico solo vea la información de *su* institución asignada. Esto se aplica a nivel de contexto de base de datos, lo que significa que el desarrollador no necesita recordar filtrar manualmente cada consulta (`Where(x => x.Institucion == ...)`), reduciendo a cero el riesgo de fugas de información inter-institucional.
*   **Independencia de Infraestructura Front-end:** El uso de herramientas "vanilla" (editor HTML nativo, sin grandes frameworks JS acoplados) y el alojamiento local de dependencias (Chart.js) garantizan que el sistema sea extremadamente rápido y no dependa de CDNs externos (vital en redes gubernamentales restringidas).
*   **Resiliencia en Migración:** Los módulos de importación desde el antiguo portal (Supabase) están diseñados para ser **idempotentes**, permitiendo su ejecución repetida sin riesgo de duplicar la data.

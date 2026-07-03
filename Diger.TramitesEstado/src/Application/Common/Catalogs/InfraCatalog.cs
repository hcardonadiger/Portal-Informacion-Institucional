namespace Diger.TramitesEstado.Application.Common.Catalogs;

/// <summary>Catálogos estáticos de infraestructura SOL (portados del formulario original).</summary>
public static class InfraCatalog
{
    public static readonly string[] Perfiles =
    [
        "Administrador/a de redes",
        "Administrador/a de base de datos",
        "Administrador/a de servidores",
        "Especialista en ciberseguridad",
        "Desarrollador/a o configurador/a SOL",
        "Soporte técnico y mesa de ayuda"
    ];

    public static readonly string[] DatacenterCond =
    [
        "Energía redundante / UPS", "Climatización controlada", "Copias de seguridad",
        "Monitoreo 24/7", "Firewall y segmentación", "Plan de recuperación ante desastres"
    ];

    public sealed record InfraGrupo(string Grupo, string[] Items);

    public static readonly InfraGrupo[] Reqs =
    [
        new("Servidor de base de datos — Hardware",
        [
            "2 procesadores Intel Xeon Silver 4210R o capacidad equivalente",
            "64 GB RAM (2 x 32 GB RDIMM, 3200 MT/s)",
            "RAID 10 con controlador equivalente a PERC H330",
            "4 discos de 1.2 TB SAS 10K RPM",
            "Fuente redundante hot-plug 1+1, 495 W", "Red dual port 10 Gb Base-T"
        ]),
        new("Servidor de base de datos — Software",
        [
            "Windows Server 2019 Standard, 16 CORE o versión compatible",
            "Licenciamiento adicional de Windows Server por núcleos",
            "Microsoft SQL Server 2022 Standard por núcleos", "Cantidad mínima: 1 servidor"
        ]),
        new("Servidor de aplicaciones web — Hardware",
        [
            "2 procesadores Intel Xeon Silver 4210R o capacidad equivalente",
            "64 GB RAM (2 x 32 GB RDIMM, 3200 MT/s)",
            "RAID 1 con controlador equivalente a PERC H330",
            "2 discos de 1.2 TB SAS 10K RPM",
            "Fuente redundante hot-plug 1+1, 495 W", "Red dual port 10 Gb Base-T"
        ]),
        new("Servidor de aplicaciones web — Software",
        [
            "Windows Server 2019 Standard, 16 CORE o versión compatible",
            "Licenciamiento adicional de Windows Server por núcleos", "Cantidad mínima: 1 servidor"
        ]),
        new("Equivalencia para servidores virtuales",
        [
            "Núcleos de procesamiento al menos coincidentes", "Memoria RAM al menos coincidente",
            "Almacenamiento disponible al menos coincidente", "Software y licenciamiento vigentes"
        ])
    ];
}

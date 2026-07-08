USE [master]
GO
/****** Object:  Database [DigerTramitesEstado_Dev]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE DATABASE [DigerTramitesEstado_Dev]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'DigerTramitesEstado_Dev', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\DigerTramitesEstado_Dev.mdf' , SIZE = 8192KB , MAXSIZE = UNLIMITED, FILEGROWTH = 65536KB )
 LOG ON 
( NAME = N'DigerTramitesEstado_Dev_log', FILENAME = N'C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS\MSSQL\DATA\DigerTramitesEstado_Dev_log.ldf' , SIZE = 8192KB , MAXSIZE = 2048GB , FILEGROWTH = 65536KB )
 WITH CATALOG_COLLATION = DATABASE_DEFAULT, LEDGER = OFF
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET COMPATIBILITY_LEVEL = 160
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [DigerTramitesEstado_Dev].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET ARITHABORT OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET AUTO_CLOSE ON 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET  ENABLE_BROKER 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET READ_COMMITTED_SNAPSHOT ON 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET RECOVERY SIMPLE 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET  MULTI_USER 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET DB_CHAINING OFF 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET TARGET_RECOVERY_TIME = 60 SECONDS 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET DELAYED_DURABILITY = DISABLED 
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET ACCELERATED_DATABASE_RECOVERY = OFF  
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET QUERY_STORE = ON
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET QUERY_STORE (OPERATION_MODE = READ_WRITE, CLEANUP_POLICY = (STALE_QUERY_THRESHOLD_DAYS = 30), DATA_FLUSH_INTERVAL_SECONDS = 900, INTERVAL_LENGTH_MINUTES = 60, MAX_STORAGE_SIZE_MB = 1000, QUERY_CAPTURE_MODE = AUTO, SIZE_BASED_CLEANUP_MODE = AUTO, MAX_PLANS_PER_QUERY = 200, WAIT_STATS_CAPTURE_MODE = ON)
GO
USE [DigerTramitesEstado_Dev]
GO
/****** Object:  Table [dbo].[__EFMigrationsHistory]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[__EFMigrationsHistory](
	[MigrationId] [nvarchar](150) NOT NULL,
	[ProductVersion] [nvarchar](32) NOT NULL,
 CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY CLUSTERED 
(
	[MigrationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AcuerdosReunion]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AcuerdosReunion](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ReunionId] [int] NOT NULL,
	[Orden] [int] NOT NULL,
	[Compromiso] [nvarchar](500) NOT NULL,
	[Responsable] [nvarchar](200) NULL,
	[Plazo] [date] NULL,
	[Estado] [nvarchar](20) NOT NULL,
	[FechaCumplimiento] [date] NULL,
	[NotaSeguimiento] [nvarchar](1000) NULL,
	[SeguimientoActualizadoEl] [datetime2](7) NULL,
	[SeguimientoActualizadoPor] [nvarchar](150) NULL,
 CONSTRAINT [PK_AcuerdosReunion] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Areas]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Areas](
	[Id] [nvarchar](120) NOT NULL,
	[InstitucionId] [nvarchar](120) NOT NULL,
	[Nombre] [nvarchar](120) NOT NULL,
	[Descripcion] [nvarchar](max) NULL,
	[NombreCorto] [nvarchar](max) NULL,
	[LogoUrl] [nvarchar](max) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_Areas] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[AsignacionesUsuario]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[AsignacionesUsuario](
	[Id] [uniqueidentifier] NOT NULL,
	[UsuarioId] [uniqueidentifier] NOT NULL,
	[InstitucionId] [nvarchar](120) NOT NULL,
	[AreaId] [nvarchar](120) NULL,
	[UnidadId] [nvarchar](120) NULL,
	[Rol] [nvarchar](60) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_AsignacionesUsuario] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Asistentes]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Asistentes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ReunionId] [int] NOT NULL,
	[Nombre] [nvarchar](150) NOT NULL,
	[Cargo] [nvarchar](150) NULL,
	[Institucion] [nvarchar](120) NULL,
	[Departamento] [nvarchar](150) NULL,
	[Correo] [nvarchar](200) NULL,
	[Telefono] [nvarchar](40) NULL,
	[AutoRegistro] [bit] NOT NULL,
	[RegistradoEl] [datetime2](7) NULL,
 CONSTRAINT [PK_Asistentes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[CategoriasTicket]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[CategoriasTicket](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Nombre] [nvarchar](80) NOT NULL,
	[Activo] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_CategoriasTicket] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Contactos]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Contactos](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[InstitucionId] [nvarchar](120) NOT NULL,
	[AreaId] [nvarchar](120) NULL,
	[UnidadId] [nvarchar](120) NULL,
	[Institucion] [nvarchar](120) NOT NULL,
	[Nombre] [nvarchar](150) NOT NULL,
	[Cargo] [nvarchar](150) NULL,
	[Correo] [nvarchar](200) NULL,
	[Telefono] [nvarchar](40) NULL,
	[Notas] [nvarchar](1000) NULL,
	[Origen] [nvarchar](20) NOT NULL,
	[IsDeleted] [bit] NOT NULL DEFAULT 0,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_Contactos] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DocumentosInternos]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentosInternos](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[Orden] [int] NOT NULL,
	[Documento] [nvarchar](300) NOT NULL,
	[Area] [nvarchar](200) NULL,
	[Obs] [nvarchar](1000) NULL,
 CONSTRAINT [PK_DocumentosInternos] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[DocumentosSolicitados]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentosSolicitados](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[Orden] [int] NOT NULL,
	[Nombre] [nvarchar](300) NOT NULL,
	[Tipo] [nvarchar](60) NULL,
	[Recibido] [bit] NOT NULL,
	[Fecha] [date] NULL,
	[Url] [nvarchar](600) NULL,
 CONSTRAINT [PK_DocumentosSolicitados] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ExpedienteEtapaAvances]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ExpedienteEtapaAvances](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[TramiteIndex] [int] NOT NULL,
	[SubId] [nvarchar](20) NOT NULL,
	[Estado] [int] NOT NULL,
 CONSTRAINT [PK_ExpedienteEtapaAvances] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Expedientes]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Expedientes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Codigo] [nvarchar](40) NOT NULL,
	[InstitucionId] [nvarchar](120) NOT NULL,
	[AreaId] [nvarchar](120) NULL,
	[UnidadId] [nvarchar](120) NULL,
	[Institucion] [nvarchar](120) NOT NULL,
	[OrigenExternoId] [nvarchar](120) NULL,
	[FechaApertura] [date] NULL,
	[Analista] [nvarchar](150) NOT NULL,
	[DirSede] [nvarchar](300) NULL,
	[NumTramitesProd] [int] NOT NULL,
	[ContactoNombre] [nvarchar](150) NULL,
	[ContactoCargo] [nvarchar](150) NULL,
	[ContactoCorreo] [nvarchar](200) NULL,
	[ContactoTel] [nvarchar](40) NULL,
	[ObsLegal] [nvarchar](4000) NULL,
	[NumFuncionarios] [int] NULL,
	[VolumenAnual] [int] NULL,
	[TiempoObservado] [nvarchar](100) NULL,
	[TiempoNorma] [nvarchar](100) NULL,
	[DescProceso] [nvarchar](4000) NULL,
	[DocsAdicionales] [nvarchar](2000) NULL,
	[ObsFlujo] [nvarchar](2000) NULL,
	[FuncionariosDig] [int] NULL,
	[TiempoDig] [nvarchar](100) NULL,
	[ObsModelo] [nvarchar](2000) NULL,
	[InfraPersonal] [nvarchar](30) NULL,
	[InfraPersonalTI] [int] NULL,
	[InfraRespSol] [nvarchar](200) NULL,
	[InfraAcomp] [nvarchar](10) NULL,
	[InfraDcModalidad] [nvarchar](60) NULL,
	[InfraDcVirt] [nvarchar](60) NULL,
	[InfraDcVirtOtro] [nvarchar](120) NULL,
	[InfraDcDisp] [nvarchar](60) NULL,
	[InfraDcObs] [nvarchar](2000) NULL,
	[InfraPlan] [nvarchar](4000) NULL,
	[EstadoExpediente] [nvarchar](30) NOT NULL,
	[EstadoLevantamiento] [nvarchar](30) NULL,
	[ObsExpediente] [nvarchar](2000) NULL,
	[ObsLevantamiento] [nvarchar](2000) NULL,
	[ValidadoDiger] [nvarchar](150) NULL,
	[ValidadoInst] [nvarchar](200) NULL,
	[FechaValidacion] [date] NULL,
	[NumActa] [nvarchar](60) NULL,
	[IsDeleted] [bit] NOT NULL DEFAULT 0,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_Expedientes] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ExpedienteSecciones]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ExpedienteSecciones](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[Seccion] [int] NOT NULL,
	[Estado] [nvarchar](20) NOT NULL,
	[Nota] [nvarchar](500) NULL,
 CONSTRAINT [PK_ExpedienteSecciones] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[ExpedienteTramites]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ExpedienteTramites](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[TramiteIndex] [int] NOT NULL,
	[NombreTramite] [nvarchar](400) NOT NULL,
	[NombreCorto] [nvarchar](120) NULL,
	[AreaResponsable] [nvarchar](200) NULL,
	[Modalidad] [nvarchar](60) NULL,
	[PlazoLegal] [nvarchar](100) NULL,
	[Tercero] [nvarchar](200) NULL,
	[TiempoReal] [nvarchar](100) NULL,
	[MetodoPago] [nvarchar](60) NULL,
	[PagoBanco] [nvarchar](120) NULL,
	[PagoCuenta] [nvarchar](60) NULL,
	[TgrInst] [nvarchar](200) NULL,
	[TgrRubro] [nvarchar](200) NULL,
	[TgrMonto] [nvarchar](60) NULL,
	[DocEntregado] [nvarchar](300) NULL,
	[Objetivo] [nvarchar](2000) NULL,
	[Alcance] [nvarchar](60) NULL,
	[AlcanceObs] [nvarchar](2000) NULL,
	[Descripcion] [nvarchar](4000) NULL,
	[Dirigido] [nvarchar](300) NULL,
	[Horario] [nvarchar](120) NULL,
	[Telefono] [nvarchar](60) NULL,
	[EmailTramite] [nvarchar](200) NULL,
	[SitioWeb] [nvarchar](300) NULL,
 CONSTRAINT [PK_ExpedienteTramites] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FlujoNodos]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FlujoNodos](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[TramiteIndex] [int] NOT NULL,
	[Fase] [nvarchar](20) NOT NULL,
	[Orden] [int] NOT NULL,
	[Tipo] [nvarchar](20) NOT NULL,
	[Titulo] [nvarchar](300) NULL,
	[Area] [nvarchar](200) NULL,
	[Tiempo] [nvarchar](100) NULL,
	[DocEmitido] [nvarchar](300) NULL,
	[Obs] [nvarchar](2000) NULL,
	[RetornoA] [nvarchar](100) NULL,
 CONSTRAINT [PK_FlujoNodos] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FundamentosLegales]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FundamentosLegales](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[Orden] [int] NOT NULL,
	[Instrumento] [nvarchar](400) NOT NULL,
	[Articulos] [nvarchar](300) NULL,
	[Obs] [nvarchar](1000) NULL,
 CONSTRAINT [PK_FundamentosLegales] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InfraChecklist]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InfraChecklist](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[Orden] [int] NOT NULL,
	[Grupo] [nvarchar](120) NOT NULL,
	[Requisito] [nvarchar](300) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[Obs] [nvarchar](1000) NULL,
 CONSTRAINT [PK_InfraChecklist] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InfraCondiciones]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InfraCondiciones](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[Condicion] [nvarchar](120) NOT NULL,
 CONSTRAINT [PK_InfraCondiciones] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[InfraPerfiles]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[InfraPerfiles](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[Perfil] [nvarchar](120) NOT NULL,
	[Nombre] [nvarchar](150) NULL,
	[Correo] [nvarchar](200) NULL,
 CONSTRAINT [PK_InfraPerfiles] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Instituciones]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Instituciones](
	[Id] [nvarchar](120) NOT NULL,
	[Nombre] [nvarchar](120) NOT NULL,
	[Descripcion] [nvarchar](max) NULL,
	[NombreCorto] [nvarchar](max) NULL,
	[LogoUrl] [nvarchar](max) NULL,
	[InfoExtra] [nvarchar](max) NULL,
	[Activo] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_Instituciones] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Movimientos]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Movimientos](
	[Id] [nvarchar](120) NOT NULL,
	[Nombre] [nvarchar](120) NOT NULL,
	[Descripcion] [nvarchar](max) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_Movimientos] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Prefijos]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Prefijos](
	[PrefijoInstitucion] [nvarchar](120) NOT NULL,
	[PrefijoMovimiento] [nvarchar](120) NOT NULL,
	[UltimoValor] [int] NOT NULL,
	[UltimoCodigo] [nvarchar](max) NULL,
 CONSTRAINT [PK_Prefijos] PRIMARY KEY CLUSTERED 
(
	[PrefijoInstitucion] ASC,
	[PrefijoMovimiento] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Reuniones]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Reuniones](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Titulo] [nvarchar](250) NOT NULL,
	[OrigenExternoId] [nvarchar](60) NULL,
	[Visibilidad] [nvarchar](20) NOT NULL,
	[CreadoPorId] [uniqueidentifier] NULL,
	[RegistroToken] [uniqueidentifier] NOT NULL,
	[RegistroAbierto] [bit] NOT NULL,
	[Fecha] [date] NULL,
	[Hora] [nvarchar](20) NULL,
	[Duracion] [nvarchar](60) NULL,
	[Modalidad] [nvarchar](40) NULL,
	[Lugar] [nvarchar](250) NULL,
	[InstitucionId] [nvarchar](120) NULL,
	[AreaId] [nvarchar](120) NULL,
	[UnidadId] [nvarchar](120) NULL,
	[Institucion] [nvarchar](120) NULL,
	[Tipo] [nvarchar](60) NULL,
	[EsCapacitacionPlataforma] [bit] NOT NULL,
	[ObjetivoAgenda] [nvarchar](4000) NULL,
	[Desarrollo] [nvarchar](max) NULL,
	[Tema] [nvarchar](250) NULL,
	[ObjetivoCap] [nvarchar](2000) NULL,
	[Contenido] [nvarchar](4000) NULL,
	[EpNombre] [nvarchar](150) NULL,
	[EpCargo] [nvarchar](150) NULL,
	[EpCorreo] [nvarchar](200) NULL,
	[EpTel] [nvarchar](40) NULL,
	[FacNombre] [nvarchar](150) NULL,
	[FacCargo] [nvarchar](150) NULL,
	[FacCorreo] [nvarchar](200) NULL,
	[Convocados] [int] NULL,
	[NumAsistentes] [int] NULL,
	[PctAsistencia] [int] NULL,
	[Satisfaccion] [nvarchar](60) NULL,
	[Compromisos] [nvarchar](4000) NULL,
	[ValDiger] [nvarchar](200) NULL,
	[ValInst] [nvarchar](200) NULL,
	[DocsRecursos] [nvarchar](4000) NULL,
	[Foto1Url] [nvarchar](600) NULL,
	[Foto1Desc] [nvarchar](300) NULL,
	[Foto2Url] [nvarchar](600) NULL,
	[Foto2Desc] [nvarchar](300) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
	[IsDeleted] [bit] NOT NULL DEFAULT 0,
 CONSTRAINT [PK_Reuniones] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[RolModuloAccesos]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[RolModuloAccesos](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Rol] [nvarchar](20) NOT NULL,
	[Modulo] [nvarchar](40) NOT NULL,
 CONSTRAINT [PK_RolModuloAccesos] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TemasTicket]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TemasTicket](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Nombre] [nvarchar](80) NOT NULL,
	[HorasResolucion] [int] NOT NULL,
	[Activo] [bit] NOT NULL,
	[CategoriaId] [int] NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_TemasTicket] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TicketAdjuntos]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TicketAdjuntos](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TicketId] [int] NOT NULL,
	[ComentarioId] [int] NULL,
	[NombreArchivo] [nvarchar](260) NOT NULL,
	[Url] [nvarchar](600) NOT NULL,
	[Tamano] [bigint] NOT NULL,
 CONSTRAINT [PK_TicketAdjuntos] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TicketComentarios]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TicketComentarios](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TicketId] [int] NOT NULL,
	[Tipo] [nvarchar](20) NOT NULL,
	[Autor] [nvarchar](150) NOT NULL,
	[Texto] [nvarchar](2000) NOT NULL,
	[Fecha] [datetime2](7) NOT NULL,
 CONSTRAINT [PK_TicketComentarios] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Tickets]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Tickets](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Numero] [nvarchar](30) NOT NULL,
	[Titulo] [nvarchar](200) NOT NULL,
	[Descripcion] [nvarchar](4000) NULL,
	[TemaId] [int] NULL,
	[Prioridad] [nvarchar](20) NOT NULL,
	[Estado] [nvarchar](20) NOT NULL,
	[InstitucionId] [nvarchar](120) NULL,
	[AreaId] [nvarchar](120) NULL,
	[UnidadId] [nvarchar](120) NULL,
	[Institucion] [nvarchar](120) NULL,
	[ExpedienteId] [int] NULL,
	[ExpedienteCodigo] [nvarchar](40) NULL,
	[ReportanteNombre] [nvarchar](150) NULL,
	[ReportanteCorreo] [nvarchar](200) NULL,
	[ReportanteTelefono] [nvarchar](40) NULL,
	[CreadoPorId] [uniqueidentifier] NULL,
	[CreadoPor] [nvarchar](150) NULL,
	[AsignadoAId] [uniqueidentifier] NULL,
	[AsignadoA] [nvarchar](150) NULL,
	[FechaResolucion] [datetime2](7) NULL,
	[NotaResolucion] [nvarchar](2000) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
	[IsDeleted] [bit] NOT NULL DEFAULT 0,
 CONSTRAINT [PK_Tickets] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TicketTramites]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TicketTramites](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[TicketId] [int] NOT NULL,
	[TramiteDefinicionId] [int] NULL,
	[Tramite] [nvarchar](400) NOT NULL,
 CONSTRAINT [PK_TicketTramites] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TramiteRequisitos]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TramiteRequisitos](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[ExpedienteId] [int] NOT NULL,
	[TramiteIndex] [int] NOT NULL,
	[Orden] [int] NOT NULL,
	[Requisito] [nvarchar](500) NOT NULL,
	[Obs] [nvarchar](2000) NULL,
	[Accion] [nvarchar](30) NULL,
	[Justificacion] [nvarchar](2000) NULL,
 CONSTRAINT [PK_TramiteRequisitos] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TramitesDefinicion]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TramitesDefinicion](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[InstitucionId] [nvarchar](120) NOT NULL,
	[Nombre] [nvarchar](400) NOT NULL,
	[Orden] [int] NOT NULL,
 CONSTRAINT [PK_TramitesDefinicion] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Unidades]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Unidades](
	[Id] [nvarchar](120) NOT NULL,
	[AreaId] [nvarchar](120) NOT NULL,
	[Nombre] [nvarchar](120) NOT NULL,
	[Descripcion] [nvarchar](max) NULL,
	[NombreCorto] [nvarchar](max) NULL,
	[LogoUrl] [nvarchar](max) NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_Unidades] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Usuarios]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Usuarios](
	[Id] [uniqueidentifier] NOT NULL,
	[Nombre] [nvarchar](150) NOT NULL,
	[Correo] [nvarchar](200) NOT NULL,
	[PasswordHash] [nvarchar](300) NOT NULL,
	[Telefono] [nvarchar](max) NULL,
	[Activo] [bit] NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[CreatedBy] [nvarchar](max) NULL,
	[UpdatedAt] [datetime2](7) NULL,
	[UpdatedBy] [nvarchar](max) NULL,
 CONSTRAINT [PK_Usuarios] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[UsuarioTemas]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[UsuarioTemas](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UsuarioId] [uniqueidentifier] NOT NULL,
	[TemaId] [int] NOT NULL,
 CONSTRAINT [PK_UsuarioTemas] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AcuerdosReunion_Estado]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_AcuerdosReunion_Estado] ON [dbo].[AcuerdosReunion]
(
	[Estado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AcuerdosReunion_Plazo]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_AcuerdosReunion_Plazo] ON [dbo].[AcuerdosReunion]
(
	[Plazo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_AcuerdosReunion_ReunionId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_AcuerdosReunion_ReunionId] ON [dbo].[AcuerdosReunion]
(
	[ReunionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Areas_InstitucionId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Areas_InstitucionId] ON [dbo].[Areas]
(
	[InstitucionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AsignacionesUsuario_InstitucionId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_AsignacionesUsuario_InstitucionId] ON [dbo].[AsignacionesUsuario]
(
	[InstitucionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_AsignacionesUsuario_UsuarioId_InstitucionId_AreaId_UnidadId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_AsignacionesUsuario_UsuarioId_InstitucionId_AreaId_UnidadId] ON [dbo].[AsignacionesUsuario]
(
	[UsuarioId] ASC,
	[InstitucionId] ASC,
	[AreaId] ASC,
	[UnidadId] ASC
)
WHERE ([AreaId] IS NOT NULL AND [UnidadId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Asistentes_ReunionId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Asistentes_ReunionId] ON [dbo].[Asistentes]
(
	[ReunionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_CategoriasTicket_Nombre]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_CategoriasTicket_Nombre] ON [dbo].[CategoriasTicket]
(
	[Nombre] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Contactos_AreaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Contactos_AreaId] ON [dbo].[Contactos]
(
	[AreaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Contactos_Institucion]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Contactos_Institucion] ON [dbo].[Contactos]
(
	[Institucion] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Contactos_InstitucionId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Contactos_InstitucionId] ON [dbo].[Contactos]
(
	[InstitucionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Contactos_Nombre]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Contactos_Nombre] ON [dbo].[Contactos]
(
	[Nombre] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Contactos_UnidadId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Contactos_UnidadId] ON [dbo].[Contactos]
(
	[UnidadId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_DocumentosInternos_ExpedienteId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_DocumentosInternos_ExpedienteId] ON [dbo].[DocumentosInternos]
(
	[ExpedienteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_DocumentosSolicitados_ExpedienteId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_DocumentosSolicitados_ExpedienteId] ON [dbo].[DocumentosSolicitados]
(
	[ExpedienteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_ExpedienteEtapaAvances_ExpedienteId_TramiteIndex_SubId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_ExpedienteEtapaAvances_ExpedienteId_TramiteIndex_SubId] ON [dbo].[ExpedienteEtapaAvances]
(
	[ExpedienteId] ASC,
	[TramiteIndex] ASC,
	[SubId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Expedientes_AreaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Expedientes_AreaId] ON [dbo].[Expedientes]
(
	[AreaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Expedientes_Codigo]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Expedientes_Codigo] ON [dbo].[Expedientes]
(
	[Codigo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Expedientes_CreatedAt]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Expedientes_CreatedAt] ON [dbo].[Expedientes]
(
	[CreatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Expedientes_EstadoExpediente]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Expedientes_EstadoExpediente] ON [dbo].[Expedientes]
(
	[EstadoExpediente] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Expedientes_InstitucionId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Expedientes_InstitucionId] ON [dbo].[Expedientes]
(
	[InstitucionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Expedientes_OrigenExternoId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Expedientes_OrigenExternoId] ON [dbo].[Expedientes]
(
	[OrigenExternoId] ASC
)
WHERE ([OrigenExternoId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Expedientes_UnidadId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Expedientes_UnidadId] ON [dbo].[Expedientes]
(
	[UnidadId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ExpedienteSecciones_ExpedienteId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_ExpedienteSecciones_ExpedienteId] ON [dbo].[ExpedienteSecciones]
(
	[ExpedienteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ExpedienteTramites_ExpedienteId_TramiteIndex]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_ExpedienteTramites_ExpedienteId_TramiteIndex] ON [dbo].[ExpedienteTramites]
(
	[ExpedienteId] ASC,
	[TramiteIndex] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_FlujoNodos_ExpedienteId_TramiteIndex_Fase]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_FlujoNodos_ExpedienteId_TramiteIndex_Fase] ON [dbo].[FlujoNodos]
(
	[ExpedienteId] ASC,
	[TramiteIndex] ASC,
	[Fase] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_FundamentosLegales_ExpedienteId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_FundamentosLegales_ExpedienteId] ON [dbo].[FundamentosLegales]
(
	[ExpedienteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_InfraChecklist_ExpedienteId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_InfraChecklist_ExpedienteId] ON [dbo].[InfraChecklist]
(
	[ExpedienteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_InfraCondiciones_ExpedienteId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_InfraCondiciones_ExpedienteId] ON [dbo].[InfraCondiciones]
(
	[ExpedienteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_InfraPerfiles_ExpedienteId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_InfraPerfiles_ExpedienteId] ON [dbo].[InfraPerfiles]
(
	[ExpedienteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Instituciones_Nombre]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Instituciones_Nombre] ON [dbo].[Instituciones]
(
	[Nombre] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Reuniones_AreaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Reuniones_AreaId] ON [dbo].[Reuniones]
(
	[AreaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Reuniones_CreadoPorId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Reuniones_CreadoPorId] ON [dbo].[Reuniones]
(
	[CreadoPorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Reuniones_Fecha]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Reuniones_Fecha] ON [dbo].[Reuniones]
(
	[Fecha] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Reuniones_InstitucionId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Reuniones_InstitucionId] ON [dbo].[Reuniones]
(
	[InstitucionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Reuniones_OrigenExternoId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Reuniones_OrigenExternoId] ON [dbo].[Reuniones]
(
	[OrigenExternoId] ASC
)
WHERE ([OrigenExternoId] IS NOT NULL)
WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Reuniones_RegistroToken]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Reuniones_RegistroToken] ON [dbo].[Reuniones]
(
	[RegistroToken] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Reuniones_UnidadId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Reuniones_UnidadId] ON [dbo].[Reuniones]
(
	[UnidadId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Reuniones_Visibilidad_CreadoPorId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Reuniones_Visibilidad_CreadoPorId] ON [dbo].[Reuniones]
(
	[Visibilidad] ASC,
	[CreadoPorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_RolModuloAccesos_Rol_Modulo]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_RolModuloAccesos_Rol_Modulo] ON [dbo].[RolModuloAccesos]
(
	[Rol] ASC,
	[Modulo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TemasTicket_CategoriaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_TemasTicket_CategoriaId] ON [dbo].[TemasTicket]
(
	[CategoriaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_TemasTicket_Nombre]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_TemasTicket_Nombre] ON [dbo].[TemasTicket]
(
	[Nombre] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TicketAdjuntos_TicketId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_TicketAdjuntos_TicketId] ON [dbo].[TicketAdjuntos]
(
	[TicketId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TicketComentarios_TicketId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_TicketComentarios_TicketId] ON [dbo].[TicketComentarios]
(
	[TicketId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Tickets_AreaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_AreaId] ON [dbo].[Tickets]
(
	[AreaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Tickets_AsignadoAId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_AsignadoAId] ON [dbo].[Tickets]
(
	[AsignadoAId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Tickets_CreadoPorId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_CreadoPorId] ON [dbo].[Tickets]
(
	[CreadoPorId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Tickets_CreatedAt]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_CreatedAt] ON [dbo].[Tickets]
(
	[CreatedAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Tickets_Estado]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_Estado] ON [dbo].[Tickets]
(
	[Estado] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Tickets_ExpedienteId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_ExpedienteId] ON [dbo].[Tickets]
(
	[ExpedienteId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Tickets_InstitucionId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_InstitucionId] ON [dbo].[Tickets]
(
	[InstitucionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Tickets_Numero]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Tickets_Numero] ON [dbo].[Tickets]
(
	[Numero] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Tickets_TemaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_TemaId] ON [dbo].[Tickets]
(
	[TemaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Tickets_UnidadId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Tickets_UnidadId] ON [dbo].[Tickets]
(
	[UnidadId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TicketTramites_TicketId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_TicketTramites_TicketId] ON [dbo].[TicketTramites]
(
	[TicketId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_TramiteRequisitos_ExpedienteId_TramiteIndex]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_TramiteRequisitos_ExpedienteId_TramiteIndex] ON [dbo].[TramiteRequisitos]
(
	[ExpedienteId] ASC,
	[TramiteIndex] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_TramitesDefinicion_InstitucionId_Orden]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_TramitesDefinicion_InstitucionId_Orden] ON [dbo].[TramitesDefinicion]
(
	[InstitucionId] ASC,
	[Orden] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Unidades_AreaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_Unidades_AreaId] ON [dbo].[Unidades]
(
	[AreaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_Usuarios_Correo]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_Usuarios_Correo] ON [dbo].[Usuarios]
(
	[Correo] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_UsuarioTemas_TemaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE NONCLUSTERED INDEX [IX_UsuarioTemas_TemaId] ON [dbo].[UsuarioTemas]
(
	[TemaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_UsuarioTemas_UsuarioId_TemaId]    Script Date: 7/6/2026 4:30:24 PM ******/
CREATE UNIQUE NONCLUSTERED INDEX [IX_UsuarioTemas_UsuarioId_TemaId] ON [dbo].[UsuarioTemas]
(
	[UsuarioId] ASC,
	[TemaId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[AcuerdosReunion] ADD  DEFAULT (N'Pendiente') FOR [Estado]
GO
ALTER TABLE [dbo].[CategoriasTicket] ADD  DEFAULT (CONVERT([bit],(1))) FOR [Activo]
GO
ALTER TABLE [dbo].[Reuniones] ADD  DEFAULT (N'Publica') FOR [Visibilidad]
GO
ALTER TABLE [dbo].[Reuniones] ADD  DEFAULT (newid()) FOR [RegistroToken]
GO
ALTER TABLE [dbo].[TemasTicket] ADD  DEFAULT ((0)) FOR [HorasResolucion]
GO
ALTER TABLE [dbo].[TemasTicket] ADD  DEFAULT (CONVERT([bit],(1))) FOR [Activo]
GO
ALTER TABLE [dbo].[AcuerdosReunion]  WITH CHECK ADD  CONSTRAINT [FK_AcuerdosReunion_Reuniones_ReunionId] FOREIGN KEY([ReunionId])
REFERENCES [dbo].[Reuniones] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AcuerdosReunion] CHECK CONSTRAINT [FK_AcuerdosReunion_Reuniones_ReunionId]
GO
ALTER TABLE [dbo].[Areas]  WITH CHECK ADD  CONSTRAINT [FK_Areas_Instituciones_InstitucionId] FOREIGN KEY([InstitucionId])
REFERENCES [dbo].[Instituciones] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Areas] CHECK CONSTRAINT [FK_Areas_Instituciones_InstitucionId]
GO
ALTER TABLE [dbo].[AsignacionesUsuario]  WITH CHECK ADD  CONSTRAINT [FK_AsignacionesUsuario_Instituciones_InstitucionId] FOREIGN KEY([InstitucionId])
REFERENCES [dbo].[Instituciones] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AsignacionesUsuario] CHECK CONSTRAINT [FK_AsignacionesUsuario_Instituciones_InstitucionId]
GO
ALTER TABLE [dbo].[AsignacionesUsuario]  WITH CHECK ADD  CONSTRAINT [FK_AsignacionesUsuario_Usuarios_UsuarioId] FOREIGN KEY([UsuarioId])
REFERENCES [dbo].[Usuarios] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[AsignacionesUsuario] CHECK CONSTRAINT [FK_AsignacionesUsuario_Usuarios_UsuarioId]
GO
ALTER TABLE [dbo].[Asistentes]  WITH CHECK ADD  CONSTRAINT [FK_Asistentes_Reuniones_ReunionId] FOREIGN KEY([ReunionId])
REFERENCES [dbo].[Reuniones] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Asistentes] CHECK CONSTRAINT [FK_Asistentes_Reuniones_ReunionId]
GO
ALTER TABLE [dbo].[Contactos]  WITH CHECK ADD  CONSTRAINT [FK_Contactos_Areas_AreaId] FOREIGN KEY([AreaId])
REFERENCES [dbo].[Areas] ([Id])
GO
ALTER TABLE [dbo].[Contactos] CHECK CONSTRAINT [FK_Contactos_Areas_AreaId]
GO
ALTER TABLE [dbo].[Contactos]  WITH CHECK ADD  CONSTRAINT [FK_Contactos_Instituciones_InstitucionId] FOREIGN KEY([InstitucionId])
REFERENCES [dbo].[Instituciones] ([Id])
GO
ALTER TABLE [dbo].[Contactos] CHECK CONSTRAINT [FK_Contactos_Instituciones_InstitucionId]
GO
ALTER TABLE [dbo].[Contactos]  WITH CHECK ADD  CONSTRAINT [FK_Contactos_Unidades_UnidadId] FOREIGN KEY([UnidadId])
REFERENCES [dbo].[Unidades] ([Id])
GO
ALTER TABLE [dbo].[Contactos] CHECK CONSTRAINT [FK_Contactos_Unidades_UnidadId]
GO
ALTER TABLE [dbo].[DocumentosInternos]  WITH CHECK ADD  CONSTRAINT [FK_DocumentosInternos_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[DocumentosInternos] CHECK CONSTRAINT [FK_DocumentosInternos_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[DocumentosSolicitados]  WITH CHECK ADD  CONSTRAINT [FK_DocumentosSolicitados_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[DocumentosSolicitados] CHECK CONSTRAINT [FK_DocumentosSolicitados_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[ExpedienteEtapaAvances]  WITH CHECK ADD  CONSTRAINT [FK_ExpedienteEtapaAvances_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ExpedienteEtapaAvances] CHECK CONSTRAINT [FK_ExpedienteEtapaAvances_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[Expedientes]  WITH CHECK ADD  CONSTRAINT [FK_Expedientes_Areas_AreaId] FOREIGN KEY([AreaId])
REFERENCES [dbo].[Areas] ([Id])
GO
ALTER TABLE [dbo].[Expedientes] CHECK CONSTRAINT [FK_Expedientes_Areas_AreaId]
GO
ALTER TABLE [dbo].[Expedientes]  WITH CHECK ADD  CONSTRAINT [FK_Expedientes_Instituciones_InstitucionId] FOREIGN KEY([InstitucionId])
REFERENCES [dbo].[Instituciones] ([Id])
GO
ALTER TABLE [dbo].[Expedientes] CHECK CONSTRAINT [FK_Expedientes_Instituciones_InstitucionId]
GO
ALTER TABLE [dbo].[Expedientes]  WITH CHECK ADD  CONSTRAINT [FK_Expedientes_Unidades_UnidadId] FOREIGN KEY([UnidadId])
REFERENCES [dbo].[Unidades] ([Id])
GO
ALTER TABLE [dbo].[Expedientes] CHECK CONSTRAINT [FK_Expedientes_Unidades_UnidadId]
GO
ALTER TABLE [dbo].[ExpedienteSecciones]  WITH CHECK ADD  CONSTRAINT [FK_ExpedienteSecciones_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ExpedienteSecciones] CHECK CONSTRAINT [FK_ExpedienteSecciones_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[ExpedienteTramites]  WITH CHECK ADD  CONSTRAINT [FK_ExpedienteTramites_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[ExpedienteTramites] CHECK CONSTRAINT [FK_ExpedienteTramites_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[FlujoNodos]  WITH CHECK ADD  CONSTRAINT [FK_FlujoNodos_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[FlujoNodos] CHECK CONSTRAINT [FK_FlujoNodos_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[FundamentosLegales]  WITH CHECK ADD  CONSTRAINT [FK_FundamentosLegales_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[FundamentosLegales] CHECK CONSTRAINT [FK_FundamentosLegales_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[InfraChecklist]  WITH CHECK ADD  CONSTRAINT [FK_InfraChecklist_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[InfraChecklist] CHECK CONSTRAINT [FK_InfraChecklist_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[InfraCondiciones]  WITH CHECK ADD  CONSTRAINT [FK_InfraCondiciones_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[InfraCondiciones] CHECK CONSTRAINT [FK_InfraCondiciones_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[InfraPerfiles]  WITH CHECK ADD  CONSTRAINT [FK_InfraPerfiles_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[InfraPerfiles] CHECK CONSTRAINT [FK_InfraPerfiles_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[Reuniones]  WITH CHECK ADD  CONSTRAINT [FK_Reuniones_Areas_AreaId] FOREIGN KEY([AreaId])
REFERENCES [dbo].[Areas] ([Id])
GO
ALTER TABLE [dbo].[Reuniones] CHECK CONSTRAINT [FK_Reuniones_Areas_AreaId]
GO
ALTER TABLE [dbo].[Reuniones]  WITH CHECK ADD  CONSTRAINT [FK_Reuniones_Instituciones_InstitucionId] FOREIGN KEY([InstitucionId])
REFERENCES [dbo].[Instituciones] ([Id])
GO
ALTER TABLE [dbo].[Reuniones] CHECK CONSTRAINT [FK_Reuniones_Instituciones_InstitucionId]
GO
ALTER TABLE [dbo].[Reuniones]  WITH CHECK ADD  CONSTRAINT [FK_Reuniones_Unidades_UnidadId] FOREIGN KEY([UnidadId])
REFERENCES [dbo].[Unidades] ([Id])
GO
ALTER TABLE [dbo].[Reuniones] CHECK CONSTRAINT [FK_Reuniones_Unidades_UnidadId]
GO
ALTER TABLE [dbo].[Reuniones]  WITH CHECK ADD  CONSTRAINT [FK_Reuniones_Usuarios_CreadoPorId] FOREIGN KEY([CreadoPorId])
REFERENCES [dbo].[Usuarios] ([Id])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Reuniones] CHECK CONSTRAINT [FK_Reuniones_Usuarios_CreadoPorId]
GO
ALTER TABLE [dbo].[TemasTicket]  WITH CHECK ADD  CONSTRAINT [FK_TemasTicket_CategoriasTicket_CategoriaId] FOREIGN KEY([CategoriaId])
REFERENCES [dbo].[CategoriasTicket] ([Id])
GO
ALTER TABLE [dbo].[TemasTicket] CHECK CONSTRAINT [FK_TemasTicket_CategoriasTicket_CategoriaId]
GO
ALTER TABLE [dbo].[TicketAdjuntos]  WITH CHECK ADD  CONSTRAINT [FK_TicketAdjuntos_Tickets_TicketId] FOREIGN KEY([TicketId])
REFERENCES [dbo].[Tickets] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TicketAdjuntos] CHECK CONSTRAINT [FK_TicketAdjuntos_Tickets_TicketId]
GO
ALTER TABLE [dbo].[TicketComentarios]  WITH CHECK ADD  CONSTRAINT [FK_TicketComentarios_Tickets_TicketId] FOREIGN KEY([TicketId])
REFERENCES [dbo].[Tickets] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TicketComentarios] CHECK CONSTRAINT [FK_TicketComentarios_Tickets_TicketId]
GO
ALTER TABLE [dbo].[Tickets]  WITH CHECK ADD  CONSTRAINT [FK_Tickets_Areas_AreaId] FOREIGN KEY([AreaId])
REFERENCES [dbo].[Areas] ([Id])
GO
ALTER TABLE [dbo].[Tickets] CHECK CONSTRAINT [FK_Tickets_Areas_AreaId]
GO
ALTER TABLE [dbo].[Tickets]  WITH CHECK ADD  CONSTRAINT [FK_Tickets_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Tickets] CHECK CONSTRAINT [FK_Tickets_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[Tickets]  WITH CHECK ADD  CONSTRAINT [FK_Tickets_Instituciones_InstitucionId] FOREIGN KEY([InstitucionId])
REFERENCES [dbo].[Instituciones] ([Id])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Tickets] CHECK CONSTRAINT [FK_Tickets_Instituciones_InstitucionId]
GO
ALTER TABLE [dbo].[Tickets]  WITH CHECK ADD  CONSTRAINT [FK_Tickets_TemasTicket_TemaId] FOREIGN KEY([TemaId])
REFERENCES [dbo].[TemasTicket] ([Id])
GO
ALTER TABLE [dbo].[Tickets] CHECK CONSTRAINT [FK_Tickets_TemasTicket_TemaId]
GO
ALTER TABLE [dbo].[Tickets]  WITH CHECK ADD  CONSTRAINT [FK_Tickets_Unidades_UnidadId] FOREIGN KEY([UnidadId])
REFERENCES [dbo].[Unidades] ([Id])
GO
ALTER TABLE [dbo].[Tickets] CHECK CONSTRAINT [FK_Tickets_Unidades_UnidadId]
GO
ALTER TABLE [dbo].[Tickets]  WITH CHECK ADD  CONSTRAINT [FK_Tickets_Usuarios_AsignadoAId] FOREIGN KEY([AsignadoAId])
REFERENCES [dbo].[Usuarios] ([Id])
ON DELETE SET NULL
GO
ALTER TABLE [dbo].[Tickets] CHECK CONSTRAINT [FK_Tickets_Usuarios_AsignadoAId]
GO
ALTER TABLE [dbo].[Tickets]  WITH CHECK ADD  CONSTRAINT [FK_Tickets_Usuarios_CreadoPorId] FOREIGN KEY([CreadoPorId])
REFERENCES [dbo].[Usuarios] ([Id])
GO
ALTER TABLE [dbo].[Tickets] CHECK CONSTRAINT [FK_Tickets_Usuarios_CreadoPorId]
GO
ALTER TABLE [dbo].[TicketTramites]  WITH CHECK ADD  CONSTRAINT [FK_TicketTramites_Tickets_TicketId] FOREIGN KEY([TicketId])
REFERENCES [dbo].[Tickets] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TicketTramites] CHECK CONSTRAINT [FK_TicketTramites_Tickets_TicketId]
GO
ALTER TABLE [dbo].[TramiteRequisitos]  WITH CHECK ADD  CONSTRAINT [FK_TramiteRequisitos_Expedientes_ExpedienteId] FOREIGN KEY([ExpedienteId])
REFERENCES [dbo].[Expedientes] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TramiteRequisitos] CHECK CONSTRAINT [FK_TramiteRequisitos_Expedientes_ExpedienteId]
GO
ALTER TABLE [dbo].[TramitesDefinicion]  WITH CHECK ADD  CONSTRAINT [FK_TramitesDefinicion_Instituciones_InstitucionId] FOREIGN KEY([InstitucionId])
REFERENCES [dbo].[Instituciones] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TramitesDefinicion] CHECK CONSTRAINT [FK_TramitesDefinicion_Instituciones_InstitucionId]
GO
ALTER TABLE [dbo].[Unidades]  WITH CHECK ADD  CONSTRAINT [FK_Unidades_Areas_AreaId] FOREIGN KEY([AreaId])
REFERENCES [dbo].[Areas] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Unidades] CHECK CONSTRAINT [FK_Unidades_Areas_AreaId]
GO
ALTER TABLE [dbo].[UsuarioTemas]  WITH CHECK ADD  CONSTRAINT [FK_UsuarioTemas_TemasTicket_TemaId] FOREIGN KEY([TemaId])
REFERENCES [dbo].[TemasTicket] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UsuarioTemas] CHECK CONSTRAINT [FK_UsuarioTemas_TemasTicket_TemaId]
GO
ALTER TABLE [dbo].[UsuarioTemas]  WITH CHECK ADD  CONSTRAINT [FK_UsuarioTemas_Usuarios_UsuarioId] FOREIGN KEY([UsuarioId])
REFERENCES [dbo].[Usuarios] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[UsuarioTemas] CHECK CONSTRAINT [FK_UsuarioTemas_Usuarios_UsuarioId]
GO
/****** Object:  StoredProcedure [dbo].[SP_GenerarCodigoMovimiento]    Script Date: 7/6/2026 4:30:24 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE PROCEDURE [dbo].[SP_GenerarCodigoMovimiento]
        @Institucion VARCHAR(50),
        @Movimiento VARCHAR(50),
        @NuevoCodigo VARCHAR(100) OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;
        
        BEGIN TRANSACTION;
        
        DECLARE @ActualValor INT = 0;
        
        -- UPDLOCK para evitar concurrencia
        SELECT @ActualValor = UltimoValor
        FROM Prefijos WITH (UPDLOCK, HOLDLOCK)
        WHERE PrefijoInstitucion = @Institucion AND PrefijoMovimiento = @Movimiento;
        
        IF @@ROWCOUNT = 0
        BEGIN
            SET @ActualValor = 1;
            SET @NuevoCodigo = @Institucion + '-' + @Movimiento + '-1';
            
            INSERT INTO Prefijos (PrefijoInstitucion, PrefijoMovimiento, UltimoValor, UltimoCodigo)
            VALUES (@Institucion, @Movimiento, @ActualValor, @NuevoCodigo);
        END
        ELSE
        BEGIN
            SET @ActualValor = @ActualValor + 1;
            SET @NuevoCodigo = @Institucion + '-' + @Movimiento + '-' + CAST(@ActualValor AS VARCHAR(20));
            
            UPDATE Prefijos
            SET UltimoValor = @ActualValor,
                UltimoCodigo = @NuevoCodigo
            WHERE PrefijoInstitucion = @Institucion AND PrefijoMovimiento = @Movimiento;
        END
        
        COMMIT TRANSACTION;
    END
GO
USE [master]
GO
ALTER DATABASE [DigerTramitesEstado_Dev] SET  READ_WRITE 
GO

-- =====================================================================
-- KITSUNE HRM - Esquema MySQL 8  (Fase 1: seguridad, organización, empleados)
-- =====================================================================
CREATE DATABASE IF NOT EXISTS kitsune_hrm
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE kitsune_hrm;

SET FOREIGN_KEY_CHECKS = 0;

-- ---------------------------------------------------------------------
-- SEGURIDAD
-- ---------------------------------------------------------------------
CREATE TABLE roles (
  id            INT AUTO_INCREMENT PRIMARY KEY,
  nombre        VARCHAR(50)  NOT NULL UNIQUE,      -- Administrador, RRHH, Supervisor, Empleado
  descripcion   VARCHAR(200) NULL,
  activo        TINYINT(1)   NOT NULL DEFAULT 1,
  creado_en     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE permisos (
  id            INT AUTO_INCREMENT PRIMARY KEY,
  codigo        VARCHAR(80)  NOT NULL UNIQUE,      -- ej. empleados.ver, empleados.crear
  modulo        VARCHAR(50)  NOT NULL,
  descripcion   VARCHAR(200) NULL
) ENGINE=InnoDB;

CREATE TABLE roles_permisos (
  rol_id        INT NOT NULL,
  permiso_id    INT NOT NULL,
  PRIMARY KEY (rol_id, permiso_id),
  CONSTRAINT fk_rp_rol     FOREIGN KEY (rol_id)     REFERENCES roles(id)    ON DELETE CASCADE,
  CONSTRAINT fk_rp_permiso FOREIGN KEY (permiso_id) REFERENCES permisos(id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE usuarios (
  id                    INT AUTO_INCREMENT PRIMARY KEY,
  username              VARCHAR(50)  NOT NULL UNIQUE,
  email                 VARCHAR(150) NOT NULL UNIQUE,
  password_hash         VARCHAR(100) NOT NULL,                 -- BCrypt
  rol_id                INT          NOT NULL,
  empleado_id           INT          NULL,
  activo                TINYINT(1)   NOT NULL DEFAULT 1,       -- activo / inactivo
  bloqueado             TINYINT(1)   NOT NULL DEFAULT 0,
  intentos_fallidos     INT          NOT NULL DEFAULT 0,
  bloqueado_hasta       DATETIME     NULL,                     -- bloqueo temporal
  debe_cambiar_password TINYINT(1)   NOT NULL DEFAULT 0,
  password_cambiada_en  DATETIME     NULL,
  ultimo_login          DATETIME     NULL,
  ultimo_login_ip       VARCHAR(45)  NULL,
  creado_en             DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  actualizado_en        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_usr_rol FOREIGN KEY (rol_id) REFERENCES roles(id),
  INDEX ix_usr_activo (activo, bloqueado)
) ENGINE=InnoDB;

CREATE TABLE refresh_tokens (
  id            BIGINT AUTO_INCREMENT PRIMARY KEY,
  usuario_id    INT          NOT NULL,
  token_hash    CHAR(64)     NOT NULL UNIQUE,                  -- SHA-256 del token
  expira_en     DATETIME     NOT NULL,
  creado_en     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  creado_ip     VARCHAR(45)  NULL,
  revocado_en   DATETIME     NULL,
  reemplazado_por CHAR(64)   NULL,
  CONSTRAINT fk_rt_usr FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE,
  INDEX ix_rt_usr (usuario_id)
) ENGINE=InnoDB;

-- Registro de accesos (login ok / fallido / bloqueo / logout)
CREATE TABLE registro_accesos (
  id            BIGINT AUTO_INCREMENT PRIMARY KEY,
  usuario_id    INT          NULL,
  username      VARCHAR(50)  NOT NULL,
  evento        VARCHAR(30)  NOT NULL,   -- LOGIN_OK, LOGIN_FALLIDO, BLOQUEO, DESBLOQUEO, LOGOUT, TOKEN_REFRESH
  exitoso       TINYINT(1)   NOT NULL,
  detalle       VARCHAR(250) NULL,
  ip            VARCHAR(45)  NULL,
  user_agent    VARCHAR(300) NULL,
  fecha         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_ra_usr (usuario_id),
  INDEX ix_ra_fecha (fecha),
  INDEX ix_ra_evento (evento)
) ENGINE=InnoDB;

-- Auditoría de operaciones (quién cambió qué, antes/después)
CREATE TABLE auditoria (
  id              BIGINT AUTO_INCREMENT PRIMARY KEY,
  usuario_id      INT          NULL,
  username        VARCHAR(50)  NULL,
  accion          VARCHAR(30)  NOT NULL,   -- CREAR, ACTUALIZAR, ELIMINAR, ACTIVAR, DESACTIVAR, DESBLOQUEAR, EXPORTAR...
  entidad         VARCHAR(80)  NOT NULL,
  entidad_id      VARCHAR(40)  NULL,
  valores_antes   JSON         NULL,
  valores_despues JSON         NULL,
  ip              VARCHAR(45)  NULL,
  fecha           DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_au_usr (usuario_id),
  INDEX ix_au_entidad (entidad, entidad_id),
  INDEX ix_au_fecha (fecha)
) ENGINE=InnoDB;

-- Logs técnicos de la aplicación (errores, advertencias)
CREATE TABLE logs_sistema (
  id            BIGINT AUTO_INCREMENT PRIMARY KEY,
  nivel         VARCHAR(12)  NOT NULL,   -- INFO, WARN, ERROR
  origen        VARCHAR(120) NULL,
  mensaje       VARCHAR(500) NOT NULL,
  excepcion     TEXT         NULL,
  usuario_id    INT          NULL,
  ruta          VARCHAR(250) NULL,
  metodo        VARCHAR(10)  NULL,
  ip            VARCHAR(45)  NULL,
  fecha         DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_log_nivel (nivel),
  INDEX ix_log_fecha (fecha)
) ENGINE=InnoDB;

CREATE TABLE configuracion (
  clave         VARCHAR(80)  PRIMARY KEY,
  valor         VARCHAR(500) NOT NULL,
  descripcion   VARCHAR(200) NULL
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- ORGANIZACIÓN
-- ---------------------------------------------------------------------
CREATE TABLE sucursales (
  id          INT AUTO_INCREMENT PRIMARY KEY,
  nombre      VARCHAR(100) NOT NULL UNIQUE,
  direccion   VARCHAR(250) NULL,
  telefono    VARCHAR(30)  NULL,
  activo      TINYINT(1)   NOT NULL DEFAULT 1
) ENGINE=InnoDB;

CREATE TABLE departamentos (
  id                INT AUTO_INCREMENT PRIMARY KEY,
  nombre            VARCHAR(100) NOT NULL UNIQUE,
  descripcion       VARCHAR(250) NULL,
  departamento_padre_id INT      NULL,
  jefe_empleado_id  INT          NULL,
  activo            TINYINT(1)   NOT NULL DEFAULT 1,
  CONSTRAINT fk_dep_padre FOREIGN KEY (departamento_padre_id) REFERENCES departamentos(id)
) ENGINE=InnoDB;

CREATE TABLE puestos (
  id              INT AUTO_INCREMENT PRIMARY KEY,
  nombre          VARCHAR(100) NOT NULL,
  departamento_id INT          NOT NULL,
  salario_minimo  DECIMAL(12,2) NULL,
  salario_maximo  DECIMAL(12,2) NULL,
  activo          TINYINT(1)   NOT NULL DEFAULT 1,
  UNIQUE KEY uq_puesto (nombre, departamento_id),
  CONSTRAINT fk_pue_dep FOREIGN KEY (departamento_id) REFERENCES departamentos(id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- EMPLEADOS
-- ---------------------------------------------------------------------
CREATE TABLE empleados (
  id                 INT AUTO_INCREMENT PRIMARY KEY,
  codigo             VARCHAR(20)  NOT NULL UNIQUE,
  nombres            VARCHAR(100) NOT NULL,
  apellidos          VARCHAR(100) NOT NULL,
  dpi                CHAR(13)     NOT NULL UNIQUE,
  nit                VARCHAR(15)  NULL UNIQUE,
  fecha_nacimiento   DATE         NOT NULL,
  genero             CHAR(1)      NULL,                     -- M / F / O
  estado_civil       VARCHAR(20)  NULL,
  email              VARCHAR(150) NULL,
  telefono           VARCHAR(30)  NULL,
  direccion          VARCHAR(250) NULL,
  fotografia_url     VARCHAR(300) NULL,
  contacto_emergencia_nombre   VARCHAR(120) NULL,
  contacto_emergencia_telefono VARCHAR(30)  NULL,
  contacto_emergencia_parentesco VARCHAR(50) NULL,
  fecha_ingreso      DATE         NOT NULL,
  fecha_baja         DATE         NULL,
  estado_laboral     VARCHAR(20)  NOT NULL DEFAULT 'ACTIVO', -- ACTIVO, INACTIVO, SUSPENDIDO, VACACIONES, RETIRADO
  activo             TINYINT(1)   NOT NULL DEFAULT 1,
  departamento_id    INT          NULL,
  puesto_id          INT          NULL,
  sucursal_id        INT          NULL,
  jefe_id            INT          NULL,
  creado_por         INT          NULL,
  creado_en          DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  actualizado_en     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  CONSTRAINT fk_emp_dep  FOREIGN KEY (departamento_id) REFERENCES departamentos(id),
  CONSTRAINT fk_emp_pue  FOREIGN KEY (puesto_id)       REFERENCES puestos(id),
  CONSTRAINT fk_emp_suc  FOREIGN KEY (sucursal_id)     REFERENCES sucursales(id),
  CONSTRAINT fk_emp_jefe FOREIGN KEY (jefe_id)         REFERENCES empleados(id),
  INDEX ix_emp_nombre (apellidos, nombres),
  INDEX ix_emp_estado (activo, estado_laboral),
  INDEX ix_emp_nac (fecha_nacimiento)
) ENGINE=InnoDB;

ALTER TABLE usuarios      ADD CONSTRAINT fk_usr_emp FOREIGN KEY (empleado_id)    REFERENCES empleados(id);
ALTER TABLE departamentos ADD CONSTRAINT fk_dep_jefe FOREIGN KEY (jefe_empleado_id) REFERENCES empleados(id);

CREATE TABLE contratos (
  id               INT AUTO_INCREMENT PRIMARY KEY,
  empleado_id      INT          NOT NULL,
  tipo             VARCHAR(30)  NOT NULL,        -- INDEFINIDO, PLAZO_FIJO, POR_OBRA, TEMPORAL, PRUEBA
  fecha_inicio     DATE         NOT NULL,
  fecha_fin        DATE         NULL,
  salario          DECIMAL(12,2) NOT NULL,
  estado           VARCHAR(20)  NOT NULL DEFAULT 'VIGENTE',   -- VIGENTE, VENCIDO, RENOVADO, RESCINDIDO
  documento_url    VARCHAR(300) NULL,
  observaciones    VARCHAR(500) NULL,
  creado_en        DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_con_emp FOREIGN KEY (empleado_id) REFERENCES empleados(id),
  INDEX ix_con_fin (estado, fecha_fin)
) ENGINE=InnoDB;

CREATE TABLE ausencias (
  id             INT AUTO_INCREMENT PRIMARY KEY,
  empleado_id    INT          NOT NULL,
  tipo           VARCHAR(30)  NOT NULL,   -- VACACIONES, PERMISO, INCAPACIDAD, FALTA
  fecha_inicio   DATE         NOT NULL,
  fecha_fin      DATE         NOT NULL,
  motivo         VARCHAR(300) NULL,
  estado         VARCHAR(20)  NOT NULL DEFAULT 'PENDIENTE',   -- PENDIENTE, APROBADA, RECHAZADA
  aprobado_por   INT          NULL,
  creado_en      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_aus_emp FOREIGN KEY (empleado_id) REFERENCES empleados(id),
  INDEX ix_aus_fechas (estado, fecha_inicio, fecha_fin)
) ENGINE=InnoDB;

SET FOREIGN_KEY_CHECKS = 1;

-- =====================================================================
-- KITSUNE HRM - Fase 2 (migración incremental)
-- Ejecutar DESPUÉS de 01, 02 y 03. Es idempotente: se puede correr más de una vez.
-- No incluye CREATE DATABASE ni USE: ejecútelo sobre la base ya seleccionada.
-- =====================================================================

-- ---------------------------------------------------------------------
-- Horarios y asistencia
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS horarios (
  id             INT AUTO_INCREMENT PRIMARY KEY,
  nombre         VARCHAR(80) NOT NULL UNIQUE,
  hora_entrada   TIME        NOT NULL,
  hora_salida    TIME        NOT NULL,
  tolerancia_min INT         NOT NULL DEFAULT 10,
  dias_laborales VARCHAR(20) NOT NULL DEFAULT '1,2,3,4,5',   -- 1=lunes ... 7=domingo
  activo         TINYINT(1)  NOT NULL DEFAULT 1
) ENGINE=InnoDB;

INSERT INTO horarios (nombre, hora_entrada, hora_salida, tolerancia_min, dias_laborales)
SELECT 'Jornada diurna', '08:00:00', '17:00:00', 10, '1,2,3,4,5' FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM horarios);

CREATE TABLE IF NOT EXISTS asistencias (
  id               BIGINT AUTO_INCREMENT PRIMARY KEY,
  empleado_id      INT           NOT NULL,
  fecha            DATE          NOT NULL,
  hora_entrada     DATETIME      NULL,        -- hora local de Guatemala
  hora_salida      DATETIME      NULL,
  minutos_retardo  INT           NOT NULL DEFAULT 0,
  horas_trabajadas DECIMAL(6,2)  NOT NULL DEFAULT 0,
  horas_extra      DECIMAL(6,2)  NOT NULL DEFAULT 0,
  estado           VARCHAR(15)   NOT NULL DEFAULT 'PRESENTE',   -- PRESENTE, RETARDO, FALTA
  origen           VARCHAR(10)   NOT NULL DEFAULT 'MARCA',      -- MARCA, MANUAL, SISTEMA
  observaciones    VARCHAR(300)  NULL,
  creado_en        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_asist (empleado_id, fecha),
  INDEX ix_asist_fecha (fecha),
  CONSTRAINT fk_asist_emp FOREIGN KEY (empleado_id) REFERENCES empleados(id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- Columnas nuevas en tablas existentes (condicionales)
-- ---------------------------------------------------------------------
SET @s = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE empleados ADD COLUMN horario_id INT NULL', 'SELECT 1')
          FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'empleados' AND column_name = 'horario_id');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @s = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE ausencias ADD COLUMN dias INT NOT NULL DEFAULT 0', 'SELECT 1')
          FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'ausencias' AND column_name = 'dias');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @s = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE ausencias ADD COLUMN comentario_resolucion VARCHAR(300) NULL', 'SELECT 1')
          FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'ausencias' AND column_name = 'comentario_resolucion');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @s = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE ausencias ADD COLUMN resuelto_en DATETIME NULL', 'SELECT 1')
          FROM information_schema.columns WHERE table_schema = DATABASE() AND table_name = 'ausencias' AND column_name = 'resuelto_en');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

SET @s = (SELECT IF(COUNT(*) = 0, 'ALTER TABLE empleados ADD CONSTRAINT fk_emp_horario FOREIGN KEY (horario_id) REFERENCES horarios(id)', 'SELECT 1')
          FROM information_schema.table_constraints WHERE table_schema = DATABASE() AND table_name = 'empleados' AND constraint_name = 'fk_emp_horario');
PREPARE st FROM @s; EXECUTE st; DEALLOCATE PREPARE st;

-- ---------------------------------------------------------------------
-- Nómina
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS nominas (
  id               INT AUTO_INCREMENT PRIMARY KEY,
  anio             INT          NOT NULL,
  mes              INT          NOT NULL,
  fecha_inicio     DATE         NOT NULL,
  fecha_fin        DATE         NOT NULL,
  estado           VARCHAR(15)  NOT NULL DEFAULT 'BORRADOR',    -- BORRADOR, CERRADA, PAGADA
  total_devengado  DECIMAL(14,2) NOT NULL DEFAULT 0,
  total_descuentos DECIMAL(14,2) NOT NULL DEFAULT 0,
  total_neto       DECIMAL(14,2) NOT NULL DEFAULT 0,
  generado_por     INT          NULL,
  generado_en      DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  cerrado_en       DATETIME     NULL,
  UNIQUE KEY uq_nomina_periodo (anio, mes)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS nomina_detalle (
  id                    INT AUTO_INCREMENT PRIMARY KEY,
  nomina_id             INT           NOT NULL,
  empleado_id           INT           NOT NULL,
  salario_base          DECIMAL(14,2) NOT NULL DEFAULT 0,
  dias_faltados         INT           NOT NULL DEFAULT 0,
  descuento_faltas      DECIMAL(14,2) NOT NULL DEFAULT 0,
  horas_extra           DECIMAL(8,2)  NOT NULL DEFAULT 0,
  monto_horas_extra     DECIMAL(14,2) NOT NULL DEFAULT 0,
  bonificacion_incentivo DECIMAL(14,2) NOT NULL DEFAULT 0,
  otros_bonos           DECIMAL(14,2) NOT NULL DEFAULT 0,
  total_devengado       DECIMAL(14,2) NOT NULL DEFAULT 0,
  igss                  DECIMAL(14,2) NOT NULL DEFAULT 0,
  isr                   DECIMAL(14,2) NOT NULL DEFAULT 0,
  otros_descuentos      DECIMAL(14,2) NOT NULL DEFAULT 0,
  total_descuentos      DECIMAL(14,2) NOT NULL DEFAULT 0,
  neto                  DECIMAL(14,2) NOT NULL DEFAULT 0,
  observaciones         VARCHAR(300)  NULL,
  UNIQUE KEY uq_nomdet (nomina_id, empleado_id),
  CONSTRAINT fk_nomdet_nom FOREIGN KEY (nomina_id)  REFERENCES nominas(id) ON DELETE CASCADE,
  CONSTRAINT fk_nomdet_emp FOREIGN KEY (empleado_id) REFERENCES empleados(id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- Capacitación, evaluaciones y objetivos
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS capacitaciones (
  id           INT AUTO_INCREMENT PRIMARY KEY,
  nombre       VARCHAR(150) NOT NULL,
  descripcion  VARCHAR(500) NULL,
  proveedor    VARCHAR(120) NULL,
  fecha_inicio DATE         NOT NULL,
  fecha_fin    DATE         NOT NULL,
  horas        INT          NOT NULL DEFAULT 0,
  estado       VARCHAR(15)  NOT NULL DEFAULT 'PROGRAMADA',   -- PROGRAMADA, EN_CURSO, FINALIZADA, CANCELADA
  creado_en    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS capacitacion_participantes (
  id              INT AUTO_INCREMENT PRIMARY KEY,
  capacitacion_id INT          NOT NULL,
  empleado_id     INT          NOT NULL,
  estado          VARCHAR(15)  NOT NULL DEFAULT 'INSCRITO',   -- INSCRITO, COMPLETADO, NO_COMPLETADO
  calificacion    DECIMAL(5,2) NULL,
  UNIQUE KEY uq_cap_part (capacitacion_id, empleado_id),
  CONSTRAINT fk_cp_cap FOREIGN KEY (capacitacion_id) REFERENCES capacitaciones(id) ON DELETE CASCADE,
  CONSTRAINT fk_cp_emp FOREIGN KEY (empleado_id)     REFERENCES empleados(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS evaluaciones (
  id                    INT AUTO_INCREMENT PRIMARY KEY,
  empleado_id           INT          NOT NULL,
  evaluador_empleado_id INT          NULL,
  evaluador_usuario_id  INT          NULL,
  periodo               VARCHAR(30)  NOT NULL,
  fecha                 DATE         NOT NULL,
  productividad         INT          NOT NULL,
  calidad               INT          NOT NULL,
  trabajo_equipo        INT          NOT NULL,
  puntualidad           INT          NOT NULL,
  total                 DECIMAL(4,2) NOT NULL,
  comentarios           VARCHAR(1000) NULL,
  creado_en             DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_eval_emp (empleado_id),
  CONSTRAINT fk_eval_emp FOREIGN KEY (empleado_id) REFERENCES empleados(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS objetivos (
  id           INT AUTO_INCREMENT PRIMARY KEY,
  empleado_id  INT          NOT NULL,
  titulo       VARCHAR(150) NOT NULL,
  descripcion  VARCHAR(500) NULL,
  fecha_limite DATE         NULL,
  progreso     INT          NOT NULL DEFAULT 0,
  estado       VARCHAR(15)  NOT NULL DEFAULT 'PENDIENTE',    -- PENDIENTE, EN_PROGRESO, CUMPLIDO, NO_CUMPLIDO
  creado_por   INT          NULL,
  creado_en    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_obj_emp (empleado_id),
  CONSTRAINT fk_obj_emp FOREIGN KEY (empleado_id) REFERENCES empleados(id)
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- Bajas y liquidaciones
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS bajas (
  id            INT AUTO_INCREMENT PRIMARY KEY,
  empleado_id   INT          NOT NULL,
  tipo          VARCHAR(30)  NOT NULL,   -- RENUNCIA, DESPIDO_JUSTIFICADO, DESPIDO_INJUSTIFICADO, FIN_CONTRATO, MUTUO_ACUERDO
  fecha_baja    DATE         NOT NULL,
  motivo        VARCHAR(300) NULL,
  observaciones VARCHAR(500) NULL,
  registrado_por INT         NULL,
  creado_en     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  INDEX ix_baja_emp (empleado_id),
  CONSTRAINT fk_baja_emp FOREIGN KEY (empleado_id) REFERENCES empleados(id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS liquidaciones (
  id                INT AUTO_INCREMENT PRIMARY KEY,
  baja_id           INT           NOT NULL UNIQUE,
  salario_base      DECIMAL(14,2) NOT NULL DEFAULT 0,
  anios_servicio    DECIMAL(6,2)  NOT NULL DEFAULT 0,
  indemnizacion     DECIMAL(14,2) NOT NULL DEFAULT 0,
  aguinaldo         DECIMAL(14,2) NOT NULL DEFAULT 0,
  bono14            DECIMAL(14,2) NOT NULL DEFAULT 0,
  vacaciones_dias   DECIMAL(6,2)  NOT NULL DEFAULT 0,
  vacaciones_monto  DECIMAL(14,2) NOT NULL DEFAULT 0,
  salario_pendiente DECIMAL(14,2) NOT NULL DEFAULT 0,
  otros_ingresos    DECIMAL(14,2) NOT NULL DEFAULT 0,
  descuentos        DECIMAL(14,2) NOT NULL DEFAULT 0,
  total             DECIMAL(14,2) NOT NULL DEFAULT 0,
  observaciones     VARCHAR(500)  NULL,
  creado_en         DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  CONSTRAINT fk_liq_baja FOREIGN KEY (baja_id) REFERENCES bajas(id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- ---------------------------------------------------------------------
-- Parámetros configurables (verificar con contabilidad / asesoría legal)
-- ---------------------------------------------------------------------
INSERT IGNORE INTO configuracion (clave, valor, descripcion) VALUES
 ('nomina.igss_laboral',          '0.0483', 'Cuota laboral IGSS (fracción)'),
 ('nomina.bonificacion_incentivo','250',    'Bonificación incentivo mensual (Q)'),
 ('nomina.factor_hora_extra',     '1.5',    'Factor de pago de la hora extra'),
 ('vacaciones.dias_por_anio',     '15',     'Días hábiles de vacaciones por año de servicio');

-- ---------------------------------------------------------------------
-- Permisos nuevos
-- ---------------------------------------------------------------------
INSERT IGNORE INTO permisos (codigo, modulo, descripcion) VALUES
 ('perfil.ver',              'Mi espacio',    'Ver y editar mi perfil'),
 ('equipo.ver',              'Mi equipo',     'Ver mi equipo a cargo'),
 ('organizacion.ver',        'Organización',  'Ver departamentos, puestos y sucursales'),
 ('organizacion.gestionar',  'Organización',  'Gestionar departamentos, puestos y sucursales'),
 ('contratos.ver',           'Contratos',     'Ver contratos'),
 ('contratos.gestionar',     'Contratos',     'Crear y modificar contratos'),
 ('asistencia.marcar',       'Asistencia',    'Marcar mi entrada y salida'),
 ('asistencia.ver',          'Asistencia',    'Ver la asistencia de todos'),
 ('asistencia.gestionar',    'Asistencia',    'Horarios, correcciones y generación de faltas'),
 ('ausencias.solicitar',     'Ausencias',     'Solicitar vacaciones y permisos'),
 ('ausencias.ver',           'Ausencias',     'Ver las solicitudes de todos'),
 ('ausencias.aprobar',       'Ausencias',     'Aprobar solicitudes de mi equipo'),
 ('ausencias.gestionar',     'Ausencias',     'Aprobar y registrar ausencias de cualquier empleado'),
 ('nomina.propia',           'Nómina',        'Ver mis recibos de pago'),
 ('nomina.ver',              'Nómina',        'Ver nóminas'),
 ('nomina.gestionar',        'Nómina',        'Generar, ajustar y cerrar nóminas'),
 ('desarrollo.propio',       'Desarrollo',    'Ver mis capacitaciones, evaluaciones y objetivos'),
 ('capacitacion.ver',        'Capacitación',  'Ver capacitaciones y evaluaciones de todos'),
 ('capacitacion.gestionar',  'Capacitación',  'Gestionar capacitaciones'),
 ('evaluaciones.gestionar',  'Capacitación',  'Crear evaluaciones y objetivos'),
 ('bajas.ver',               'Bajas',         'Ver bajas y liquidaciones'),
 ('bajas.gestionar',         'Bajas',         'Registrar bajas y liquidaciones');

-- Administrador: todo
INSERT IGNORE INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permisos p WHERE r.nombre = 'Administrador';

-- RRHH
INSERT IGNORE INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r JOIN permisos p ON p.codigo IN (
 'dashboard.ver','empleados.ver','empleados.crear','empleados.editar','empleados.estado','reportes.ver',
 'perfil.ver','organizacion.ver','organizacion.gestionar','contratos.ver','contratos.gestionar',
 'asistencia.marcar','asistencia.ver','asistencia.gestionar',
 'ausencias.solicitar','ausencias.ver','ausencias.aprobar','ausencias.gestionar',
 'nomina.propia','nomina.ver','nomina.gestionar','desarrollo.propio',
 'capacitacion.ver','capacitacion.gestionar','evaluaciones.gestionar','bajas.ver','bajas.gestionar')
WHERE r.nombre = 'RRHH';

-- Supervisor: su equipo (ya no ve a todos los empleados)
INSERT IGNORE INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r JOIN permisos p ON p.codigo IN (
 'dashboard.ver','perfil.ver','equipo.ver','asistencia.marcar','ausencias.solicitar','ausencias.aprobar',
 'nomina.propia','desarrollo.propio','evaluaciones.gestionar')
WHERE r.nombre = 'Supervisor';

DELETE rp FROM roles_permisos rp
JOIN roles r ON r.id = rp.rol_id JOIN permisos p ON p.id = rp.permiso_id
WHERE r.nombre = 'Supervisor' AND p.codigo = 'empleados.ver';

-- Empleado: autoservicio
INSERT IGNORE INTO roles_permisos (rol_id, permiso_id)
SELECT r.id, p.id FROM roles r JOIN permisos p ON p.codigo IN (
 'dashboard.ver','perfil.ver','asistencia.marcar','ausencias.solicitar','nomina.propia','desarrollo.propio')
WHERE r.nombre = 'Empleado';

-- Verificación: permisos por rol
SELECT r.nombre AS rol, COUNT(*) AS permisos
FROM roles r JOIN roles_permisos rp ON rp.rol_id = r.id GROUP BY r.nombre ORDER BY r.id;

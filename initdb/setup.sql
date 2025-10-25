-- Create monarch databases
IF DB_ID('monarch') IS NULL
BEGIN
    CREATE DATABASE monarch;
END
GO

IF DB_ID('monapi') IS NULL
BEGIN
    CREATE DATABASE monapi;
END
GO

-- If they already exist, drop and recreate service accounts.
-- This ensures permissions are correct, and login details 
-- match the injected secrets.

USE master;
IF SUSER_ID('monarch') IS NOT NULL
BEGIN
    PRINT "Dropping pre-existing monarch account.";
    DROP LOGIN monarch;
END 
GO

IF SUSER_ID('monapi') IS NOT NULL
BEGIN
    PRINT "Dropping pre-existing monapi account.";
    DROP LOGIN monapi;
END 
GO

-- Create/update service accounts

PRINT "Creating monarch service account.";
CREATE LOGIN monarch WITH PASSWORD = N'$(MONARCH_PASSWORD)';
GO

PRINT "Creating monapi service account.";
CREATE LOGIN monapi WITH PASSWORD = N'$(MONAPI_PASSWORD)';
GO

-- Set permission levels for each service account

-- monarch account needs r/w to monarch db
USE monarch;
CREATE USER monarch FOR LOGIN monarch;
ALTER ROLE db_datareader ADD MEMBER monarch;
ALTER ROLE db_datawriter ADD MEMBER monarch;
GO

-- monapi account needs r/w to monapi db
-- monarch account needs read-only to monapi db
USE monapi;
CREATE USER monarch FOR LOGIN monarch;
CREATE USER monapi FOR LOGIN monapi;
ALTER ROLE db_datareader ADD MEMBER monapi;
ALTER ROLE db_datawriter ADD MEMBER monapi;
ALTER ROLE db_datareader ADD MEMBER monarch;


-- Create sample table

USE monapi;
IF OBJECT_ID('sampleTable', 'U') IS NULL
BEGIN
    CREATE TABLE sampleTable (
        id INT PRIMARY KEY IDENTITY(1,1),
        resourceName VARCHAR(50) NOT NULL,
        currentStatus VARCHAR(20) NOT NULL,
        lastUpdated DATETIME DEFAULT GETDATE()
    );
END
GO

INSERT INTO sampleTable (resourceName, currentStatus) VALUES
('HV1', 'Healthy'),
('MONARCH', 'Healthy'),
('IS-DC1', 'Degraded'),
('IS-DC2', 'Healthy'),
('IS-DC3', 'Down');

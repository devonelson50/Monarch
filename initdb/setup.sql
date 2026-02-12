-- Devon Nelson
--
-- SQL script to automate database initialization. This script will create all necessary
-- databases, database users, logins, and tables. Configuration follows least privilege. 
-- This script can safely handle setting up a fresh database on first execution, or an
-- existing volume. If an existing database volume is attached, logins are deleted and 
-- recreated to ensure permissions are still as expected, and to ingest any modifications
-- to service account credentials.
--
-- Information is split into 3 separate logical databases, all hosted by the same container.
--      - KeyCloak requires a separate database as it handles its own automatic setup
--      - monarch and monapi tables are separated to minimize the database-level access from
--        each container. This allows permissions to be managed across all tables at a database
--        level, rather than applying different permissions per table.

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

IF DB_ID('keycloak') IS NULL
BEGIN
    CREATE DATABASE keycloak;
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

IF SUSER_ID('keycloak') IS NOT NULL
BEGIN
    PRINT "Dropping pre-existing keycloak account.";
    DROP LOGIN keycloak;
END 
GO

-- Drop database users as well

USE monarch;
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'monarch')
BEGIN
    DROP USER monarch;
END
GO

USE monapi;
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'monarch')
BEGIN
    DROP USER monarch;
END
GO

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'monapi')
BEGIN
    DROP USER monapi;
END
GO

USE keycloak;
IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'keycloak')
BEGIN
    DROP USER keycloak;
END
GO

-- Create/update service accounts

USE master;
PRINT "Creating monarch service account.";
CREATE LOGIN monarch WITH PASSWORD = N'$(MONARCH_PASSWORD)';
GO

PRINT "Creating monapi service account.";
CREATE LOGIN monapi WITH PASSWORD = N'$(MONAPI_PASSWORD)';
GO

PRINT "Creating keycloak service account.";
CREATE LOGIN keycloak WITH PASSWORD = N'$(KEYCLOAK_PASSWORD)';
GO

-- Set permission levels for each service account

-- keycloak user should have full access to keycloak database
USE keycloak;
CREATE USER keycloak FOR LOGIN keycloak;
ALTER ROLE db_owner ADD MEMBER keycloak;
GO

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

-- Create monapi tables 

IF OBJECT_ID('newRelicApps', 'U') IS NULL
BEGIN
    CREATE TABLE newRelicApps (
        appId INT PRIMARY KEY,
        appName VARCHAR(255) NOT NULL,
        ipAddress VARCHAR(64),
        status INT NOT NULL,
        latency INT,
        cpuUsage FLOAT,
        throughput INT,
        output VARCHAR(500),
        statusUpdateTime DATETIME,
        lastCheck DATETIME
    );END
GO

IF OBJECT_ID('newRelicIncidents', 'U') IS NULL
BEGIN
    CREATE TABLE newRelicIncidents (
        incidentId VARCHAR(100) PRIMARY KEY,
        appId INT NOT NULL,
        openTime DATETIME NOT NULL,
        closeTime DATETIME
    );
END
GO

IF OBJECT_ID('nagiosApps', 'U') IS NULL
BEGIN
    CREATE TABLE nagiosApps (
        hostObjectId INT PRIMARY KEY,
        hostName VARCHAR(255),
        displayName VARCHAR(255),
        ipAddress VARCHAR(64),
        statusUpdateTime DATETIME,
        output VARCHAR(255),
        perfData VARCHAR(255),
        currentState INT,
        lastCheck DATETIME,
        lastStateChange DATETIME,
        lastTimeUp DATETIME,
        lastTimeDown DATETIME,
        lastTimeUnreachable DATETIME,
        lastNotification DATETIME,
        latency VARCHAR(255)
    );
END
GO

-- Nagios Incidents TBD


IF OBJECT_ID('jira', 'U') IS NULL
BEGIN
    CREATE TABLE jira (
        ticketId INT PRIMARY KEY,
        incidentId INT NOT NULL,
        --Issue key from Jira. Need to store it to be able to update issues in Jira from within Monarch
        --Different from incidentId as that attribute is used to store the internal Monarch incident ID
        issueKey VARCHAR(100) NOT NULL,
        teamId INT NOT NULL,
        openTime DATETIME NOT NULL,
        closeTime DATETIME,
        summary VARCHAR(255),
        description VARCHAR(255)
    );
END
GO

IF OBJECT_ID('slack', 'U') IS NULL
BEGIN
    CREATE TABLE slack (
        messageId INT PRIMARY KEY,
        incidentId INT NOT NULL,
        channel VARCHAR(255),
        text VARCHAR(255),
        timestamp DATETIME NOT NULL
    );
END
GO

-- Create monarch tables

USE monarch;

IF OBJECT_ID('users', 'U') IS NULL
BEGIN
    CREATE TABLE users (
        userId INT PRIMARY KEY,
        name VARCHAR(100) NOT NULL,
        email VARCHAR(100) NOT NULL UNIQUE,
        role VARCHAR(100) NOT NULL,
        profilePicture VARCHAR(255)
    );
END
GO

IF OBJECT_ID('teams', 'U') IS NULL
BEGIN
    CREATE TABLE teams (
        teamId INT PRIMARY KEY,
        teamName VARCHAR(100) NOT NULL,
        slackChannel VARCHAR(255),
        jiraBoard VARCHAR(255),
        smtpGroup VARCHAR(100)
    );
END
GO

IF OBJECT_ID('userTeams', 'U') IS NULL
BEGIN
    CREATE TABLE userTeams (
        userId INT NOT NULL,
        teamId INT NOT NULL
    );
END
GO

IF OBJECT_ID('apps', 'U') IS NULL
BEGIN
    CREATE TABLE apps (
        appId INT IDENTITY(1,1) PRIMARY KEY,
        newRelicId VARCHAR(100),
        nagiosId VARCHAR(100),
        appName VARCHAR(100) NOT NULL,
        status VARCHAR(100) NOT NULL,
        mostRecentIndicentId VARCHAR(100),
        slackAlert BIT DEFAULT 0,
        jiraAlert BIT DEFAULT 0,
        smtpAlert BIT DEFAULT 0
    );
END
GO

IF OBJECT_ID('incidents', 'U') IS NULL
BEGIN
    CREATE TABLE incidents (
        incidentId INT IDENTITY(1,1) PRIMARY KEY,
        appId INT NOT NULL,
        openTime DATETIME NOT NULL,
        closeTime DATETIME
    );
END
GO

IF OBJECT_ID('appTeams', 'U') IS NULL
BEGIN
    CREATE TABLE appTeams (
        teamId INT NOT NULL,
        appId INT NOT NULL
    );
END
GO

IF OBJECT_ID('jiraWorkspaces', 'U') IS NULL
BEGIN
    CREATE TABLE jiraWorkspaces (
        workspaceKey VARCHAR(100) PRIMARY KEY,
        workspaceName VARCHAR(255) NOT NULL,
        baseUrl VARCHAR(255) NOT NULL
    );
END
GO

IF OBJECT_ID('appSlackChannels', 'U') IS NULL
BEGIN
    CREATE TABLE appSlackChannels (
        appId INT NOT NULL,
        channelKey VARCHAR(100) NOT NULL,
        PRIMARY KEY (appId, channelKey)
    );
END
GO

IF OBJECT_ID('appJiraWorkspaces', 'U') IS NULL
BEGIN
    CREATE TABLE appJiraWorkspaces (
        appId INT NOT NULL,
        workspaceKey VARCHAR(100) NOT NULL,
        PRIMARY KEY (appId, workspaceKey)
    );
END
GO
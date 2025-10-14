CREATE DATABASE monarch;
GO

USE monarch;
GO

CREATE TABLE sampleTable (
    id INT PRIMARY KEY IDENTITY(1,1),
    resourceName VARCHAR(50) NOT NULL,
    currentStatus VARCHAR(20) NOT NULL,
    lastUpdated DATETIME DEFAULT GETDATE()
);

INSERT INTO sampleTable (resourceName, currentStatus) VALUES
('HV1', 'Healthy'),
('MONARCH', 'Healthy'),
('IS-DC1', 'Degraded'),
('IS-DC2', 'Healthy'),
('IS-DC3', 'Down');


SELECT * FROM monarch..sampleTable;

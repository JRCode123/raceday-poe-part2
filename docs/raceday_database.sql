-- ============================================================
-- RaceDay Database Script
-- Student: Junior | IIE Rosebank College
-- Compatible with: SQL Server Management Studio (SSMS) 2022
-- ============================================================

-- Create the database only if it does not already exist
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'RaceDay')
BEGIN
    CREATE DATABASE RaceDay;
END
GO

USE RaceDay;
GO

-- Drop all tables in reverse FK order so constraints do not block us
IF OBJECT_ID('dbo.Results',      'U') IS NOT NULL DROP TABLE dbo.Results;
IF OBJECT_ID('dbo.Enrolments',   'U') IS NOT NULL DROP TABLE dbo.Enrolments;
IF OBJECT_ID('dbo.Events',       'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID('dbo.Categories',   'U') IS NOT NULL DROP TABLE dbo.Categories;
IF OBJECT_ID('dbo.Participants', 'U') IS NOT NULL DROP TABLE dbo.Participants;
IF OBJECT_ID('dbo.Organisers',   'U') IS NOT NULL DROP TABLE dbo.Organisers;
IF OBJECT_ID('dbo.Users',        'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- TABLE 1: Users
-- Base account for all users. [Role] decides Organiser or Participant.
CREATE TABLE Users (
    UserID       INT           IDENTITY(1,1) PRIMARY KEY,
    FullName     NVARCHAR(100) NOT NULL,
    Email        NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    [Role]       NVARCHAR(20)  NOT NULL CHECK ([Role] IN ('Organiser', 'Participant')),
    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
);
GO

-- TABLE 2: Organisers - extra profile for Organiser users (1:1 with Users)
CREATE TABLE Organisers (
    OrganiserID   INT           IDENTITY(1,1) PRIMARY KEY,
    UserID        INT           NOT NULL UNIQUE,
    Organisation  NVARCHAR(150) NOT NULL,
    ContactPhone  NVARCHAR(20)  NULL,
    CONSTRAINT FK_Organisers_Users FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
GO

-- TABLE 3: Participants - extra profile for Participant users (1:1 with Users)
CREATE TABLE Participants (
    ParticipantID    INT           IDENTITY(1,1) PRIMARY KEY,
    UserID           INT           NOT NULL UNIQUE,
    DateOfBirth      DATE          NULL,
    Gender           NVARCHAR(10)  NULL CHECK (Gender IN ('Male', 'Female', 'Other')),
    EmergencyContact NVARCHAR(100) NULL,
    CONSTRAINT FK_Participants_Users FOREIGN KEY (UserID) REFERENCES Users(UserID)
);
GO

-- TABLE 4: Categories - event types created by Organisers
CREATE TABLE Categories (
    CategoryID   INT           IDENTITY(1,1) PRIMARY KEY,
    OrganiserID  INT           NOT NULL,
    [Name]       NVARCHAR(100) NOT NULL,
    Description  NVARCHAR(500) NULL,
    Sport        NVARCHAR(50)  NOT NULL CHECK (Sport IN ('Running', 'Cycling', 'Walking')),
    CONSTRAINT FK_Categories_Organisers FOREIGN KEY (OrganiserID)
        REFERENCES Organisers(OrganiserID) ON DELETE CASCADE
);
GO

-- TABLE 5: Events - a specific race e.g. Soweto Marathon 2026
CREATE TABLE Events (
    EventID         INT            IDENTITY(1,1) PRIMARY KEY,
    OrganiserID     INT            NOT NULL,
    CategoryID      INT            NOT NULL,
    [Name]          NVARCHAR(150)  NOT NULL,
    Description     NVARCHAR(1000) NULL,
    Location        NVARCHAR(200)  NOT NULL,
    Route           NVARCHAR(500)  NULL,
    EventDate       DATE           NOT NULL,
    StartTime       TIME(0)        NOT NULL,
    MaxParticipants INT            NOT NULL DEFAULT 500,
    EntryFee        DECIMAL(10,2)  NOT NULL DEFAULT 0.00,
    IsActive        BIT            NOT NULL DEFAULT 1,
    CONSTRAINT FK_Events_Organisers FOREIGN KEY (OrganiserID) REFERENCES Organisers(OrganiserID),
    CONSTRAINT FK_Events_Categories FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID)
);
GO

-- TABLE 6: Enrolments - many-to-many bridge between Participants and Events
-- No CASCADE on FKs to prevent SQL Server Error 1785 (multiple cascade paths)
CREATE TABLE Enrolments (
    EnrolmentID   INT          IDENTITY(1,1) PRIMARY KEY,
    ParticipantID INT          NOT NULL,
    EventID       INT          NOT NULL,
    EnrolledAt    DATETIME     NOT NULL DEFAULT GETDATE(),
    [Status]      NVARCHAR(20) NOT NULL DEFAULT 'Confirmed'
                  CHECK ([Status] IN ('Confirmed', 'Cancelled', 'Waitlisted')),
    CONSTRAINT UQ_Enrolments UNIQUE (ParticipantID, EventID),
    CONSTRAINT FK_Enrolments_Participants FOREIGN KEY (ParticipantID) REFERENCES Participants(ParticipantID),
    CONSTRAINT FK_Enrolments_Events FOREIGN KEY (EventID) REFERENCES Events(EventID)
);
GO

-- TABLE 7: Results - post-race results captured by Organisers (1:1 with Enrolments)
CREATE TABLE Results (
    ResultID    INT           IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID INT           NOT NULL UNIQUE,
    FinishTime  NVARCHAR(20)  NOT NULL,
    [Position]  INT           NULL,
    Notes       NVARCHAR(500) NULL,
    RecordedAt  DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentID)
        REFERENCES Enrolments(EnrolmentID) ON DELETE CASCADE
);
GO

-- ============================================================
-- SEED DATA
-- ============================================================

-- Users: 2 Organisers + 4 Participants
INSERT INTO Users (FullName, Email, PasswordHash, [Role]) VALUES
('Sipho Dlamini',  'sipho@raceza.co.za',    'hash_sipho_001',  'Organiser'),
('Fatima Mokoena', 'fatima@capesport.co.za','hash_fatima_002', 'Organiser'),
('Lebo Khumalo',   'lebo@gmail.com',        'hash_lebo_003',   'Participant'),
('Thabo Nkosi',    'thabo@gmail.com',       'hash_thabo_004',  'Participant'),
('Zanele Sithole', 'zanele@hotmail.com',    'hash_zanele_005', 'Participant'),
('David van Wyk',  'david@runner.co.za',    'hash_david_006',  'Participant');
GO

-- Organisers: UserID 1=Sipho, 2=Fatima
INSERT INTO Organisers (UserID, Organisation, ContactPhone) VALUES
(1, 'RaceZA Events',         '011 555 0101'),
(2, 'Cape Sport Collective', '021 555 0202');
GO

-- Participants: UserID 3=Lebo, 4=Thabo, 5=Zanele, 6=David
INSERT INTO Participants (UserID, DateOfBirth, Gender, EmergencyContact) VALUES
(3, '1995-03-14', 'Female', 'Mama Khumalo - 082 111 2222'),
(4, '1990-07-22', 'Male',   'Sarah Nkosi - 083 333 4444'),
(5, '1998-11-05', 'Female', 'Baba Sithole - 074 555 6666'),
(6, '1985-01-30', 'Male',   'Anna van Wyk - 071 777 8888');
GO

-- Categories
INSERT INTO Categories (OrganiserID, [Name], Description, Sport) VALUES
(1, 'Road Running', 'All on-road running events from 5km to marathon distance', 'Running'),
(1, 'Charity Walk',  'Community walking events open to all fitness levels',       'Walking'),
(2, 'Road Cycling',  'Timed road cycling events and sportive rides',              'Cycling');
GO

-- Events (minimum 3 required)
INSERT INTO Events (OrganiserID, CategoryID, [Name], Description, Location, Route, EventDate, StartTime, MaxParticipants, EntryFee) VALUES
(1, 1, 'Soweto Marathon 2026',      'The famous 42.2km marathon through the streets of Soweto.',             'Soweto, Johannesburg',  'FNB Stadium through Diepkloof to Orlando Stadium.',             '2026-11-01', '06:00', 15000, 350.00),
(1, 2, 'Joburg Charity Walk 5km',   'A fun 5km community walk raising funds for local schools.',             'Zoo Lake, Johannesburg', 'Flat circular route around Zoo Lake Park.',                     '2026-09-20', '07:30', 500,   0.00),
(2, 3, 'Cape Town Cycle Tour 109km','South Africa''s most iconic road cycling event around the Peninsula.', 'Cape Town, Western Cape','Grand Parade through Chapman''s Peak to Cape Town CBD.',        '2026-03-08', '07:00', 35000, 850.00);
GO

-- Enrolments (ParticipantID 1=Lebo 2=Thabo 3=Zanele 4=David)
INSERT INTO Enrolments (ParticipantID, EventID, [Status]) VALUES
(1, 1, 'Confirmed'),
(2, 1, 'Confirmed'),
(3, 1, 'Confirmed'),
(4, 1, 'Confirmed'),
(1, 2, 'Confirmed'),
(2, 3, 'Confirmed'),
(4, 3, 'Confirmed');
GO

-- Results (EnrolmentID 1-7 matching enrolments above in order)
INSERT INTO Results (EnrolmentID, FinishTime, [Position], Notes) VALUES
(1, '03:42:15', 3,  'Age category medal winner'),
(2, '04:01:30', 8,  'Personal best'),
(3, '03:55:44', 5,  'Strong finish'),
(4, '02:58:10', 1,  'Overall winner'),
(5, '00:52:10', 1,  'First finisher'),
(6, '02:15:33', 12, 'Completed the 109km'),
(7, '01:58:02', 3,  'Top 5 in age group');
GO

-- ============================================================
-- VERIFICATION QUERIES
-- ============================================================

SELECT 'Users'        AS TableName, COUNT(*) AS TotalRows FROM Users        UNION ALL
SELECT 'Organisers',               COUNT(*)              FROM Organisers    UNION ALL
SELECT 'Participants',             COUNT(*)              FROM Participants   UNION ALL
SELECT 'Categories',              COUNT(*)              FROM Categories     UNION ALL
SELECT 'Events',                  COUNT(*)              FROM Events         UNION ALL
SELECT 'Enrolments',              COUNT(*)              FROM Enrolments     UNION ALL
SELECT 'Results',                 COUNT(*)              FROM Results;
GO

SELECT e.[Name] AS EventName, e.EventDate, e.Location, e.EntryFee,
       c.[Name] AS Category, u.FullName AS OrganiserName
FROM Events e
JOIN Categories c ON e.CategoryID  = c.CategoryID
JOIN Organisers o ON e.OrganiserID = o.OrganiserID
JOIN Users      u ON o.UserID      = u.UserID;
GO

SELECT u.FullName AS Participant, ev.[Name] AS Event, en.[Status], en.EnrolledAt
FROM Enrolments en
JOIN Participants p  ON en.ParticipantID = p.ParticipantID
JOIN Users       u  ON p.UserID          = u.UserID
JOIN Events      ev ON en.EventID        = ev.EventID;
GO

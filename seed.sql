-- Seed data for Roadmap App
-- Run after: docker compose down -v && docker compose up -d && cd backend && dotnet ef database drop --force && dotnet ef migrations add V7 && dotnet run
-- Then connect to PostgreSQL and run this file:
--   psql -h localhost -U postgres -d roadmap -f seed.sql
-- Or via docker: docker exec -i roadmap-db psql -U postgres -d roadmap < seed.sql

-- The roadmap is created by SeedData.cs with this known ID:
-- 00000000-0000-0000-0000-000000000001

-- Helper: schedule template format is:
-- {"days":[1,2,3,4,5],"startMinute":540,"durationMinutes":60,"perDay":{"3":{"startMinute":600,"durationMinutes":90}}}
-- days: 0=Sun, 1=Mon, 2=Tue, 3=Wed, 4=Thu, 5=Fri, 6=Sat
-- startMinute: minutes from midnight
-- durationMinutes: duration in minutes

-- ===== ACTION ITEMS (all InProgress, all actionable, no parent) =====

INSERT INTO roadmap_nodes ("Id", "RoadmapId", "ParentId", "Title", "IsActionable", "Status", "Unit", "TotalSize", "UnitsPerHour", "PointsPerUnit", "ScheduleTemplate", "SortOrder", "CreatedAt") VALUES
-- 1: City | 1pt | 1000u | 0.33u/h | Wed 3h (23:00), Fri 3h (01:00=25h next day -> 23:00)
('10000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001', NULL, 'City', true, 'Active', 'u', 1000, 0.33, 1, '{"days":[3,5],"startMinute":1380,"durationMinutes":180}', 0, NOW()),

-- 2: Day Analysis | 8pt | 1000u | 2u/h | Every day 0.5h (24:00 -> use 23:30 = 1410)
('10000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', NULL, 'Day Analysis', true, 'Active', 'u', 1000, 2, 8, '{"days":[1,2,3,4,5,6,0],"startMinute":1410,"durationMinutes":30}', 1, NOW()),

-- 3: English Vocabulary | 4pt | 516u | 10u/h | Mon 0.5h, Wed 0.5h, Fri 0.5h, Sun 0.5h (15:00=900)
('10000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', NULL, 'English Vocabulary', true, 'Active', 'u', 516, 10, 4, '{"days":[1,3,5,0],"startMinute":900,"durationMinutes":30}', 2, NOW()),

-- 4: Professional notes | 2pt | 1000h | 1h/h | Tue 1h, Thu 1h, Sun 1h (11:00=660)
('10000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', NULL, 'Professional notes', true, 'Active', 'h', 1000, 1, 2, '{"days":[2,4,0],"startMinute":660,"durationMinutes":60}', 3, NOW()),

-- 5: GYM | 2pt | 427.5u | 0.33u/h | Tue 3h, Thu 3h, Sat 3h (19:00=1140, 21:00=1260, 16:00=960)
('10000000-0000-0000-0000-000000000005', '00000000-0000-0000-0000-000000000001', NULL, 'GYM', true, 'Active', 'u', 427.5, 0.33, 2, '{"days":[2,4,6],"startMinute":1140,"durationMinutes":180,"perDay":{"4":{"startMinute":1260,"durationMinutes":180},"6":{"startMinute":960,"durationMinutes":180}}}', 4, NOW()),

-- 6: Work-1 | 1pt | 2190h | 1h/h | Mon-Fri 2h (13:00=780)
('10000000-0000-0000-0000-000000000006', '00000000-0000-0000-0000-000000000001', NULL, 'Work-1', true, 'Active', 'h', 2190, 1, 1, '{"days":[1,2,3,4,5],"startMinute":780,"durationMinutes":120}', 5, NOW()),

-- 7: Breakfast | 1pt | 1000u | 2u/h | Every day 0.5h (10:00=600)
('10000000-0000-0000-0000-000000000007', '00000000-0000-0000-0000-000000000001', NULL, 'Breakfast', true, 'Active', 'u', 1000, 2, 1, '{"days":[1,2,3,4,5,6,0],"startMinute":600,"durationMinutes":30}', 6, NOW()),

-- 8: RoadToWork | 5pt | 1000u | 1u/h | Fri 1h (05:00=300 -> 8:00=480)
('10000000-0000-0000-0000-000000000008', '00000000-0000-0000-0000-000000000001', NULL, 'RoadToWork', true, 'Active', 'u', 1000, 1, 5, '{"days":[5],"startMinute":480,"durationMinutes":60}', 7, NOW()),

-- 9: Morning Routine | 4pt | 1000u | 2u/h | Every day 0.5h (04:00=240)
('10000000-0000-0000-0000-000000000009', '00000000-0000-0000-0000-000000000001', NULL, 'Morning Routine', true, 'Active', 'u', 1000, 2, 4, '{"days":[1,2,3,4,5,6,0],"startMinute":240,"durationMinutes":30}', 8, NOW()),

-- 10: Dinner | 1pt | 1000u | 2u/h | Mon 0.5h, Tue 0.5h, Thu 0.5h, Sat 0.5h, Sun 0.5h (20:00=1200)
('10000000-0000-0000-0000-000000000010', '00000000-0000-0000-0000-000000000001', NULL, 'Dinner', true, 'Active', 'u', 1000, 2, 1, '{"days":[1,2,4,6,0],"startMinute":1200,"durationMinutes":30}', 9, NOW()),

-- 11: Walk Morning | 6pt | 1000h | 1h/h | Mon-Wed,Thu(skip),Fri(skip),Sat,Sun 0.5h (08:00=480)
('10000000-0000-0000-0000-000000000011', '00000000-0000-0000-0000-000000000001', NULL, 'Walk Morning', true, 'Active', 'h', 1000, 1, 6, '{"days":[1,2,3,4,6,0],"startMinute":480,"durationMinutes":30}', 10, NOW()),

-- 12: Evening Routine | 4pt | 1000u | 2u/h | Every day 0.5h (24:00 -> 23:30=1410)
('10000000-0000-0000-0000-000000000012', '00000000-0000-0000-0000-000000000001', NULL, 'Evening Routine', true, 'Active', 'u', 1000, 2, 4, '{"days":[1,2,3,4,5,6,0],"startMinute":1410,"durationMinutes":30}', 11, NOW()),

-- 13: Week plan | 10pt | 1000u | 2u/h | Sun 0.5h (23:00=1380)
('10000000-0000-0000-0000-000000000013', '00000000-0000-0000-0000-000000000001', NULL, 'Week plan', true, 'Active', 'u', 1000, 2, 10, '{"days":[0],"startMinute":1380,"durationMinutes":30}', 12, NOW()),

-- 14: Morning Read | 4pt | 1000h | 1h/h | Mon 0.5h, Wed 0.5h, Fri 0.5h (05:00=300)
('10000000-0000-0000-0000-000000000014', '00000000-0000-0000-0000-000000000001', NULL, 'Morning Read', true, 'Active', 'h', 1000, 1, 4, '{"days":[1,3,5],"startMinute":300,"durationMinutes":30}', 13, NOW()),

-- 15: Thinking time | 8pt | 1000h | 1h/h | Mon,Tue,Thu,Fri,Sat,Sun 0.25h (22:00=1320)
('10000000-0000-0000-0000-000000000015', '00000000-0000-0000-0000-000000000001', NULL, 'Thinking time', true, 'Active', 'h', 1000, 1, 8, '{"days":[1,2,4,5,6,0],"startMinute":1320,"durationMinutes":15}', 14, NOW()),

-- 20: Book | 3pt | 10000 pages | 15 page/h | Mon,Wed,Fri,Sat,Sun 1h (21:00=1260, varied)
('10000000-0000-0000-0000-000000000020', '00000000-0000-0000-0000-000000000001', NULL, 'Book', true, 'Active', 'page', 10000, 15, 3, '{"days":[1,3,5,6,0],"startMinute":1260,"durationMinutes":60,"perDay":{"3":{"startMinute":1020,"durationMinutes":60},"6":{"startMinute":1380,"durationMinutes":60},"0":{"startMinute":1440,"durationMinutes":60}}}', 15, NOW()),

-- 21: Work-Retro | 6pt | 2190u | 6u/h | Mon,Tue,Thu,Fri ~10min each (18:00=1080)
('10000000-0000-0000-0000-000000000021', '00000000-0000-0000-0000-000000000001', NULL, 'Work-Retro', true, 'Active', 'u', 2190, 6, 6, '{"days":[1,2,4,5],"startMinute":1080,"durationMinutes":10}', 16, NOW()),

-- 22: Family Time | 2pt | 1000h | 1h/h | Mon,Tue,Thu,Sat,Sun 1h (23:00=1380)
('10000000-0000-0000-0000-000000000022', '00000000-0000-0000-0000-000000000001', NULL, 'Family Time', true, 'Active', 'h', 1000, 1, 2, '{"days":[1,2,4,6,0],"startMinute":1380,"durationMinutes":60}', 17, NOW()),

-- 24: English Movie | 3pt | 1000u | 0.33u/h | Sat,Sun 3h (17:00=1020)
('10000000-0000-0000-0000-000000000024', '00000000-0000-0000-0000-000000000001', NULL, 'English Movie', true, 'Active', 'u', 1000, 0.33, 3, '{"days":[6,0],"startMinute":1020,"durationMinutes":180}', 18, NOW()),

-- 26: Professional Books | 3pt | 10000 pages | 20 page/h | Mon,Wed,Fri,Sat,Sun 1h (12:00=720)
('10000000-0000-0000-0000-000000000026', '00000000-0000-0000-0000-000000000001', NULL, 'Professional Books', true, 'Active', 'page', 10000, 20, 3, '{"days":[1,3,5,6,0],"startMinute":720,"durationMinutes":60}', 19, NOW()),

-- 27: English Speaking | 4pt | 516u | 2u/h | Tue,Thu,Sat 0.5h (14:00=840)
('10000000-0000-0000-0000-000000000027', '00000000-0000-0000-0000-000000000001', NULL, 'English Speaking', true, 'Active', 'u', 516, 2, 4, '{"days":[2,4,6],"startMinute":840,"durationMinutes":30}', 20, NOW()),

-- 29: Walk Preparation | 0pt -> 6pt (from Point2) | 1000u | 4u/h | Mon-Wed,Fri,Sat,Sun 0.25h (07:00=420)
('10000000-0000-0000-0000-000000000029', '00000000-0000-0000-0000-000000000001', NULL, 'Walk Preparation', true, 'Active', 'u', 1000, 4, 6, '{"days":[1,2,3,6,0],"startMinute":420,"durationMinutes":15}', 21, NOW()),

-- 30: Waking Up | 0pt -> 6pt (from Point2) | 1000u | 4u/h | Every day 0.25h (03:00=180)
('10000000-0000-0000-0000-000000000030', '00000000-0000-0000-0000-000000000001', NULL, 'Waking Up', true, 'Active', 'u', 1000, 4, 6, '{"days":[1,2,3,4,5,6,0],"startMinute":180,"durationMinutes":15}', 22, NOW()),

-- 31: Notes | 4pt | 1000h | 1h/h | Tue,Thu,Sat,Sun 0.5h (05:00=300)
('10000000-0000-0000-0000-000000000031', '00000000-0000-0000-0000-000000000001', NULL, 'Notes', true, 'Active', 'h', 1000, 1, 4, '{"days":[2,4,6,0],"startMinute":300,"durationMinutes":30}', 23, NOW()),

-- 32: Coffee break | 4pt | 1000h | 1h/h | Mon,Tue,Thu,Sat,Sun 0.25h (16:00=960)
('10000000-0000-0000-0000-000000000032', '00000000-0000-0000-0000-000000000001', NULL, 'Coffee break', true, 'Active', 'h', 1000, 1, 4, '{"days":[1,2,4,6,0],"startMinute":960,"durationMinutes":15}', 24, NOW()),

-- 33: Work-2 | 1pt | 2190h | 1h/h | Mon-Fri 3.5h (17:00=1020)
('10000000-0000-0000-0000-000000000033', '00000000-0000-0000-0000-000000000001', NULL, 'Work-2', true, 'Active', 'h', 2190, 1, 1, '{"days":[1,2,3,4,5],"startMinute":1020,"durationMinutes":210}', 25, NOW()),

-- 34: Back Exercise | 4pt | 31u | 4u/h | Mon,Wed,Fri,Sun 0.25h (09:00=540)
('10000000-0000-0000-0000-000000000034', '00000000-0000-0000-0000-000000000001', NULL, 'Back Exercise', true, 'Active', 'u', 31, 4, 4, '{"days":[1,3,5,0],"startMinute":540,"durationMinutes":15}', 26, NOW()),

-- 35: 10 mins Cardio | 8pt | 427.5u | 4u/h | Mon,Wed,Sat 0.25h (17:00=1020, varied)
('10000000-0000-0000-0000-000000000035', '00000000-0000-0000-0000-000000000001', NULL, '10 mins Cardio', true, 'Active', 'u', 427.5, 4, 8, '{"days":[1,3,6],"startMinute":1020,"durationMinutes":15,"perDay":{"6":{"startMinute":960,"durationMinutes":15}}}', 27, NOW()),

-- 39: Social Networks | 4pt | 1000u | 4u/h | Every day 0.25h (06:00=360)
('10000000-0000-0000-0000-000000000039', '00000000-0000-0000-0000-000000000001', NULL, 'Social Networks', true, 'Active', 'u', 1000, 4, 4, '{"days":[1,2,3,4,5,6,0],"startMinute":360,"durationMinutes":15}', 28, NOW());


-- ===== HABITS =====

INSERT INTO habits ("Id", "RoadmapId", "Name", "CreatedAt") VALUES
('20000000-0000-0000-0000-000000000016', '00000000-0000-0000-0000-000000000001', 'Focus and attention', NOW()),
('20000000-0000-0000-0000-000000000017', '00000000-0000-0000-0000-000000000001', '30 secs law', NOW()),
('20000000-0000-0000-0000-000000000018', '00000000-0000-0000-0000-000000000001', '20-20-20', NOW()),
('20000000-0000-0000-0000-000000000019', '00000000-0000-0000-0000-000000000001', 'No bad snacks', NOW());

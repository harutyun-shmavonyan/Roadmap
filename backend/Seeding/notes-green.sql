-- Daily notes import for book='green' — 232 row(s) from 235 file(s)
-- Generated from: C:/Users/harut/Downloads/Green-20260623T144249Z-3-001/Green
-- Replaces the entire 'green' book with the folder's contents (re-runnable).
BEGIN;
DELETE FROM notes WHERE "Book" = 'green';

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 1, DATE '2018-08-20', '# Day 1 — 20.08.2018

- Есть массив из чисел 0, ..., n, s. Нужно получить кол-во всех элементов. Для решения нужно взять массив размера n и в i-ом индексе хранить counter.
- Есть n ступенек. Можно идти по лестницам по 1+1+... или по 1+2+1+... Сколько есть всевозможных случаев. Если осталась одна ступенька, то существует лишь один вариант. Если же две ступеньки, то 1+1 или 1+2. По вариантам 1+1+1 уже высчитали => снова один вариант => $f(n) = f(n-1) + f(n-2)$.
- Нужно проверить принадлежит ли точка треугольнику. Нужно эту точку присоединить с вершинами и высчитать сумму площадей полученных треугольников, если эта сумма > площади исходного треугольника, то не принадлежит, если =, то принадлежит, меньше не может быть.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 2, DATE '2018-08-21', '# Day 2 — 21.08.2018

- RoofTop
- Naive Bayes
- wordcloud
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 3, DATE '2018-08-22', '# Day 3 — 22.08.2018

- За спиной у продавца часто можно увидеть зеркало. Это для разыгрывания клиентов. Последние, видя себя в зеркале, перестают себя так вести.
- Если мы, при появлении некоторого человека начинаем радоваться, то этот человек начинает радоваться при нашем появлении.
- Если на каком-то собрании мы заранее знаем, что кто-то будет нас критиковать, то нужно сесть с ним рядом.
- Если хочется казаться уверенным в себе человеком, то нужно меньше сказать "я думаю", "мне кажется".
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 4, DATE '2018-08-23', '# Day 4 — 23.08.2018

- to fake somebody = to like somebody
- Нужно в разных ситуациях быть лидером, то есть трудные задачи брать на себя. Тогда остальные будут чувствовать себя комфортно, а ты будешь учиться. И тогда все станут уважать тебя и принять как лидера.
- Нельзя никого из своих друзей, знакомых или доверенных бить с тыла, даже с тыла жен. Например в фильмах "Казино", "Лицо со шрамом".
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 5, DATE '2018-08-24', '# Day 5 — 24.08.2018

- Из книги "24 законов обольщения". Самое важное в процессе обольщения "красоты" - показать, что вас бесит в ней то, на что другие не обращают внимания - ум. Нельзя вызывать у неё чувство неполноценности в той области, где она ощущает себя особенно уверенно.
- Если есть металлический контейнер, в которой вода, а на контейнер падает солнце, то уровень воды можно определить по температуре поверхности.
- Если покупаешь землю для выращивания деревьев, обязательно нужно проверить не является земля заявленной.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 6, DATE '2018-08-25', '# Day 6 — 25.08.2018

- Hydrographics (иммерсионная печать). Процесс переноса текстуры на материю с помощью воды (из Wheeler Dealers S10 E03).
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 7, DATE '2018-08-26', '# Day 7 — 26.08.2018

- Всегда старайтесь высунуть из багажника запаску и проверить под ней, чтобы проверить было ли столкновение сзади или нет. Если когда-то сбили машину, то округление не будет.
- Задача find a Peak element.
- Если в магазине одежды не удается найти подходящий размер, можно смотреть на манекенах.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 8, DATE '2018-08-27', '# Day 8 — 27.08.2018

- Термопаста должна быть нанесена тонким слоем.
- Арада resort.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 9, DATE '2018-08-28', '# Day 9 — 28.08.2018

- Зимой вечером нужно сбить снег с защитников, чтобы не замерзал.
- Если ночью забыть отщелкать дворники, то утром они замерзнут. Когда мы включаем двигатель, дворники начнут работать, но так как они замерзли, мы не можем увидеть, что моторчики работают под напряжением.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 10, DATE '2018-08-29', '# Day 10 — 29.08.2018

- Зимой ночью можно дворники поставить в вертикальном положении, чтобы не замерзли.
- Зимой замки могут замерзать. Первым делом нужно проверить остальные двери. (Такое может случиться, если машину ночью мыли).
- Для того, чтобы лобовое стекло не покрылось коркой льда, нужно до того как выйти из машины, проветрить салон, чтобы температура выравнялась.
- Зимой нельзя парковаться на луже, чтобы колеса не замерзли.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 11, DATE '2018-08-30', '# Day 11 — 30.08.2018

- IATA - International Air Transport Ass.
- ICAO - International Civil Aviation Org.
- First class, Business class, Premium Economy, Economy.
- Codeshare agreement.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 12, DATE '2018-08-31', '# Day 12 — 31.04.2018

- Если сел аккумулятор, можно вызвать такси для перезарядки.
- Не стоит машину парковать над засохшей травой, или на горючей, так как глушитель бывает очень горячим.
- Часто в колесах заправляют азот для поддерживания давления.
- Когда мотор не прогрет, вращается быстрее.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 13, DATE '2018-09-01', '# Day 13 — 01.09.2018

- При парковке на спуске или подъёме нужно проворачивать колёса в соответствующем направлении.
- Если мотор перегрелся, не нужно отключать зажигание, а нужно открыть все окна и включить печь на максимальной мощности.
- Нельзя проверять уровень масла в моторе сразу после отключения зажигания.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 14, DATE '2018-09-02', '# Day 14 — 02.05.2018

- В космосе бывает 16 закатов и восходов за день.
- Застенчивость - защита.
- Первая гонка в мире в 1894 году. Средняя скорость - 17 км/ч.
- Алекс, Шикана, Шпилька.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 15, DATE '2018-09-03', '# Day 15 — 03.09.2018

- Франсуа Клюзе, Жан Дюжарден, Венсан Кассель.
- фильм "Медуза" - Поищу другое место, подходящее моему настроению. - Какое же? - Не знаю, бар с названием "Катастрофа".
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 16, DATE '2018-09-04', '# Day 16 — 04.05.2018

- Если одновременно бросать пулю и стрелять, они коснутся земли одновременно.
- фильм "99 франков". - Пунктуальность не для креативщика.
- Бизлиалы
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 17, DATE '2018-09-05', '# Day 17 — 05.05.2018

- задача о роботах
- FIAT - Fabrica italiana automobili torino
- Французские имена женщин (самые популярные):
  - Marie - др. евр происхождение "горькая", "желанная"
  - Nathalie
  - Isabelle
  - Sylvie
  - Catherine - "вечно чистая" др греч
  - Christine
  - Monique - "единственная", "вздрагивающая"
  - Sandrine
  - Veronique - "песня" и "победа"
  - Nicole - "победительница народов"
  - Stephanie
  - Sophie - "мудрость", "знание"
  - Patricia - "благородный"
  - Brigitte - "сила", "мощь"
  - Julie
  - Aurelie
  - Jacqueline
  - Michele
- Киноальманах
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 18, DATE '2018-09-06', '# Day 18 — 06.05.2018

- 24 законов обольщения:
  - о чувстве "звезды"
  - о бесполезности открытой критики
  - об отступлении (мираж, что мысли зародились в её голове)
  - о вечеринке (искусный собеседник)
  - о том, чтобы быть "дураком"
  - об инсинуации ("внедрение")
- Ebonics - Афроамериканский английский
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 19, DATE '2018-09-07', '# Day 19 — 07.05.2018

- Прилагательные, чтобы охарактеризовать девушку:
  - Заботливая
  - Привлекательная
  - Нежная
  - Милая
  - Очаровательная
  - Обворожительная
  - Неповторимая
  - Неописуемая
  - Незабываемая
  - Неотразимая
  - Ш��карная
  - Ослепительная
  - Ангельская
  - Лучезарная
  - Яркая
  - Обольстительная
  - Утонченная
  - Энергичная
  - Стильная
  - Сказочная
  - Желанная
  - Загадочная
  - Изысканная
  - Сладкая
  - Экстравагантная
  - Прелестная
  - Дивная
  - Артистичная
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 20, DATE '2018-09-08', '# Day 20 — 08.05.2018

- В фильме "Банды Нью Йорка" в финале, когда показывается развитие Нью-Йорка, в качестве символа новой трагедии, показываются башни Всемирного торгового центра.
- Скудерия Феррари - итальянская автогоночная команда. Переводится как "Конюшня Феррари".
- Sabelt - итал. компания.
- Штаб-квартира Феррари находится в Маранелло.
- Номекс - огнестойкий материал.
- Рекламные нашивки составляют примерно 1/3 общего веса костюма.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 21, DATE '2018-09-09', '# Day 21 — 09.05.2018

- Книга "Колёса":
  - Почему продукция понедельников и пятниц нехорошая
  - Ловушка продавцов
  - Фишки на мюзиклах Чикаго
  - Лаборатория в автозаводе, где разбирают на части разные машины.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 22, DATE '2018-09-10', '# Day 22 — 10.09.2018

- Слово "Скотч" (Scotch - скупой).
- фильм "Гонка":
  - Ники Лауда
  - Джеймс Хант
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 23, DATE '2018-09-11', '# Day 23 — 11.09.2018

- фильм Дюнкерк:
  - оператор - Хойте ван Хойтема
  - звукорежиссер - Richard King
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 24, DATE '2018-09-12', '# Day 24 — 12.09.2018

- Ликёр - сод. спирта 15%-25%. Коммерческое производство началось в средние века. Ликёр обычно подают после еды с чаем или с кофе.
- Известные ликеры:
  - Амаретто (21-28%) - миндаль и некоторые секретные травы выдерживаются в коньяке. Итальянские корни, производят в Соронно. Квадратная форма. После ребрендинга называется Disaronno.
  - Бенедиктин (40%) французский крепкий ликёр. Используется 27 растений.
  - Шартрёз - фр. ликёр
  - Кюрасао
  - Шериданс - Ирландский ликёр (13%) на основе виски.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 25, DATE '2018-09-13', '# Day 25 — 13.09.2018

- Как пить ликёр:
  1. В чистом виде:
    - сладкие ликёры - дижестивы
    - горькие ликёры - аперитивы
    - температура подачи (12-20°C)
    - большинство ликёров пьют залпом
    - ЛикѓQ�ы не сочетаются с табаком / сигарами
  2. В разбавленном виде:
    - молочные продукты уместны Bp ликѓQ�ов на основе шоколада, кофе, какао / сливок
    - часто добавляют апельсиновый сок  
    - кислые соки нельзя добавлять в сливочные ликёры
  3. В сочетании другого спиртного:
    - советуется добавлять спиртное, на основе которого сделан ликѓQ�
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 26, DATE '2018-09-14', '# Day 26 — 14.09.2018

- Кленовый чай-латте:
  - чёрный чай с пряностями
  - молоко - 120 мл
  - кленовый сироп - 1 ст л.
  - корица
- Чай-латте "Лондонский туман":
  - чёрный чай "English breakfast"
  - молоко 120 мл
  - мёд - 1 ст л
  - ванильный экстракт 1/4 ст л.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 27, DATE '2018-09-15', '# Day 27 — 15.09.2018

- Чай с шоколадными каплями:
  - зелёный чай
  - молоко - 120 мл
  - ванильный сахар
  - шоколадное драже
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 28, DATE '2018-09-16', '# Day 28 — 16.09.2018

- Шейк "Тирамису":
  - пломбир 100г
  - холодный кофе 80мл
  - маскарпоне 80��
  - кофейный ликер 30 мЯ
  - шоколадный топинг', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 29, DATE '2018-09-17', '# Day 29 — 17.09.2018

- Маскарпоне - итал. сливочный сыр. Часто употребляется в тирамису.
- Тирамису - итал. "Взбодри меня".
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 30, DATE '2018-09-18', '# Day 30 — 18.09.2018

- Основные вкусовые характеристики вина. Если кислотность больше, то больше и свежесть.
- Сладость. Чем жарче климат, где рос виноград, тем выше сладость.
- Кислотность.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 31, DATE '2018-09-19', '# Day 31 — 19.09.2018

- Flairing - приготовление коктейля с использованием жонглирования.
- Необычный шот:
  - Шоколадное яйцо (Kinder)
  - сливочный ликер - 20 мл
  - виски - 15 мл
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 32, DATE '2018-09-20', '# Day 32 — 20.09.2018

- Джин - впервые начали делать в Нидерландах. Джин очень крепкии и обжигающий напиток, поэтому его пьют залпом. Его закусывают, но ни в коем случае не запивают. Джин пьют холодным.
- Известные джины:
  - Gordon''s - брит. джин. Точный состав знают только 12 человек.
  - Beefeater - брит. джин
  - Bombay Sapphire - принадлежит концерну Bacardi.
- WPAP art - Wedha''s pop art portrait
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 33, DATE '2018-09-21', '# Day 33 — 21.09.2018

- Гренадин - густой кисло-сладкий сироп красного цвета.
- Бурбон - вид виски из США. Приготавливается из кукурузы. (Jack Daniels)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 34, DATE '2018-09-22', '# Day 34 — 22.09.2018

- ASMR sound
- Sound design
- Как получают звук дождя.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 35, DATE '2018-09-23', '# Day 35 — 23.09.2018

- Reverberation
- В Исламе нельзя хранить фотографии.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 36, DATE '2018-09-24', '# Day 36 — 24.09.2018

- Сцена слухового аппарата из фильма "Дорога перемен".
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 37, DATE '2018-09-25', '# Day 37 — 25.09.2018

- Аэрография
- Dinner in the Sky.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 38, DATE '2018-09-26', '# Day 38 — 26.09.2018

- Мысли о развитии кино-театра.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 39, DATE '2018-09-27', '# Day 39 — 27.09.2018

- Пандора - первая женщина по древнегреческому мирозданию.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 40, DATE '2018-09-28', '# Day 40 — 28.09.2018

- Ящик Пандоры. Пандора, исходя из любопытства, открыла этот ящик, где Зевс положил все несчастья и беды.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 41, DATE '2018-09-29', '# Day 41 — 29.09.2018

- Дебют.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 42, DATE '2018-09-30', '# Day 42 — 30.09.2018

- Black/White Russian
- Куба-либре - ром, кока-кола, сок лайма.
- По подсчётам Bacardi ежедневно в мире выпивается 6.000.000 порций.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 43, DATE '2018-10-01', '# Day 43 — 01.10.2018

- Кровавая Мери - на основе водки и томатного сока. (Long drink). Назван по имени Марии I Тюдор (XVI век) (прозвище кровавой).
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 44, DATE '2018-10-02', '# Day 44 — 02.10.2018

- Trance:
  - GOA Trance - psychedelic trance
  - Nightcore - speeded up trance
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 45, DATE '2018-10-03', '# Day 45 — 03.10.2018

- Downtempo:
  - Chillout
  - Downbeat
  - Chill-hop
  - Chill-step
  - Trip-hop
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 46, DATE '2018-10-04', '# Day 46 — 04.10.2018

- Deep house
- Jackin'' house - focus on basses
- Bigroom house
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 47, DATE '2018-10-05', '# Day 47 — 05.10.2018

- Мокачино - американский кофейный напиток, разновидность "латте" с добавлением шоколада.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 48, DATE '2018-10-06', '# Day 48 — 06.10.2018

- Сорта вина (белые):
  - Sauvignon blanc (Совиньон-блан). Обладает большой кислотностью.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 49, DATE '2018-10-07', '# Day 49 — 07.10.2018

- Riesling (Рислинг) - немецкий белый сорт вина.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 50, DATE '2018-10-08', '# Day 50 — 08.10.2018

- Chardonnay (Шардоне) - фр. белое вино (более легкое вино).
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 51, DATE '2018-10-09', '# Day 51 — 09.10.2018

- Красные вина:
  - Pinot noir (Пино-нуар) (Бургундия)
  - Merlot
  - Cabernet sauvignon
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 52, DATE '2018-10-10', '# Day 52 — 10.10.2018

- Вопрос о собаке (Что? где? когда?)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 53, DATE '2018-10-11', '# Day 53 — 11.10.2018

- Гоночная трасса Le Man 24.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 54, DATE '2018-10-12', '# Day 54 — 12.10.2018

- Гонщики Formula 1 (кол-во чемпионств):
  - Михаэль Шумахер - 7
  - Ален Прост - 4
  - Льюис Хэмилтон - 4
  - Себастиан Фэттель - 4
  - Ники Лауда - 3
  - Фернандо Алонсо - 2
  - Джеймс Хант - 1
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 55, DATE '2018-10-13', '# Day 55 — 13.10.2018

- Кэн-Блок - гонщик рали, друг Дэна Билзериана.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 56, DATE '2018-10-14', '# Day 56 — 14.10.2018

- Сумма противоположных цифр на костях = 7.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 57, DATE '2018-10-15', '# Day 57 — 15.10.2018

- В городе, где нет света (подумать шутки для развития юмора).
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 58, DATE '2018-10-16', '# Day 58 — 16.10.2018

- Молекулярная кухня (обман вкусовых рецепторов). Термин употребляется с 1969 г.
  - Апельсиновые спагетти
  - Кофейное мясп
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 59, DATE '2018-10-17', '# Day 59 — 17.10.2018

- Гастрономический туризм:
  - Тоскана с фиорентинок), известен оливковым маслом.
  - Прованс
  - Каталония
  - Марраке�h
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 60, DATE '2018-10-18', '# Day 60 — 18.10.2018

- Крутые фотографии из ничего:
  - Фото с 1-го лица
  - Несовместимые вещи (например крутой кроссовок)
  - В нижний ракурс
  - Симметрия
  - Движения руками и ногами
  - Малое кол-во цветов
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 61, DATE '2018-10-19', '# Day 61 — 19.10.2018

- Зачем у Родда 2 тотема
- Color checker
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 62, DATE '2018-10-20', '# Day 62 — 20.10.2018

- Прежде чEм использовать компрессор для покраски, поставить машину на домкрат.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 63, DATE '2018-01-02', '# Day 63 — 02.01.2018

- Фильм "Грязь" по роману Ирвина Уэлша — Джеймс Макэвой
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 64, DATE '2018-01-03', '# Day 64 — 03.01.2018

- Гватемала на территории прежних территориях Майя.
- Почему ступенья у двери у второй гостиницы так высоко.
- Гватемала - родина шоколада. Его изобрела Майя.
- Брусчатка - шлифмашинир
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 65, DATE '2018-01-04', '# Day 65 — 04.01.2018

- Hyundai контейнеровоз
- Стандарт Панамакс
- ВB�ность Панамского канала B�я Панамы
- В Панаме единый налог для иностранных компаний — 500$ в год. Из-за этого Панама — офшорная зона-
- Офиор - это когда предприятия работают в одной стране а зарегистрированый в другой.
- Photoshop (Piximperfect):
  - flow vs opacity
  - How to edit eyes in Photoshop
- Как ѝдалить красноту глаж:
  - Mode: Lighten
  - Sample: All layers
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 66, DATE '2018-01-05', '# Day 66 — 05.01.2018

- В Чили часто бывают землетрясения из-за этого:
  - Метро на резиновых колесах
  - В Сантьяго-де-Чили 300 дней солнца
- Piximperfect whiten eyes:
  - Add ''Layers'' layer with ''screen'' blend mode
  - Add dimension with ''blend if''
- Объекты не имеют цвета — просто отражают его.
- Piximperfect change eye color — про свет и цvet

- How to get dimensioned iris (about edges)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 67, DATE '2019-01-07', '# Day 67 — 07.01.2019

- Сахарная голова Рио де Жанейро
- Храм святого Себасьяна в Рио де Жанейро
- For giving more attractiveness to photo you need to enlarge pupil of eye. (With ''liquify'' tool of Photoshop.) Not do it if: eyes are dark / there are many reflections.
- Piximperfect — How to remove eye bags:
  - Step: ''Patch tool''
  - Step: lighten with ''clone stamp tool'' with ''lighten'' blend mode.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 68, DATE '2018-01-08', '# Day 68 — 08.01.2018

- Багамы:
  - В архипелаг входит около 700 островов
  - До Фиделья Кастро Куба была курортом
  - На Багамах левостороннедвижение
  - Голубая дыра (bulue hole) - вблизи Лонг-Айленда
- Используется для экстремального дайвинга без акваланга. От переизбытка азота фридайвер теряет способность трезво оценить ситуацию. Наступает эйфория.
- Лагуна
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 69, DATE '2018-01-09', '# Day 69 — 09.01.2018

- Автобус, где можно спать лёжа.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 70, DATE '2019-06-18', '# Day 70 — 18.06.2019

- Vineyard
- maintain - поддерживать
- fragile - хрупкий
- riper - спелый
- vigorous - бодрый
- clarity - уточнить
- Stones under vine grapes (2 reasons)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 71, DATE '2019-06-21', '# Day 71 — 21.06.2019

- Steps for wine tasting:
  - Color. Watch from top to bottom on white background.
  - Smell
- 33 стр. войны — Нужно делать поправки эмоций. Если сопутствует успех, будь особенно внимателен и осторожен, если сердит не предпринимай действий, если страшно, не преувеличивай опасность.
- Суди о людях по поступкам, а не по словам.
- События, происходящие в жизни, не имеют никакого значения, если вы глубоко и всерьез не размышляете о них, а идеи, почерпнутые из книг, ничего не стоят, если не применяются в жизни.
- Intercept - перехват
- Apprehend - предчувствовать
- Decease - кончина
- screw it all up - облажаться
- to do as well - сделать так же
- just a matter of opinion - спорный вопрос
- hesitate - стесняться
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 72, DATE '2019-06-22', '# Day 72 — 22.06.2019

- 33 стр. войны:
  - Как выявить недруга (стр 48)
  - Преимущества имения недруга, образ врага (земля) (закон 1)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 73, DATE '2019-06-23', '# Day 73 — 23.06.2019

- Captivate - пленить, очаровывать
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 74, DATE '2019-06-26', '# Day 74 — 26.06.2019

- Искусство харизмы:
  - Держать ладони открытыми
  - Напряжение в кистях и пальцах
  - Быть активным слушателем
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 75, DATE '2019-06-27', '# Day 75 — 27.06.2019

- Искусство харизмы:
  - Как подарил цветы
  - Иметь большой амплитуду жестов
  - После комплимента делать шутку.
- Моне и Мане и Хаяши (are you asking me about my sexuality?)
- Эксперимент Дэвида Розенхана в больницах. Аналогия с дезинформацией.
- FBI и ЦРУ
- 33 стратегии войны — Кортес (Ацтеки, корабли, Куба) — Сунь-цзы ''Территория смерти''
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 76, DATE '2019-06-28', '# Day 76 — 28.06.2019

- Что видела собака:
  - Зализывание vs паника
  - Explicit / Implicit learning
  - ''Смертельная спираль''
  - Усовершенствование меры безопасности авиакомпаний (пример ABS и Швеции)
- Уэс Андерсон
- Pulp fiction - Винсент Вега - худший киллер в истории кино
- Inception - Ариадна внедрила идею Коббу
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 77, DATE '2019-06-30', '# Day 77 — 30.06.2019

- Фильм "The Place":
  - Паоло Дженовезе - режиссер
  - Валерио Мастандреа
  - Марко Джаллини
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 78, DATE '2019-07-01', '# Day 78 — 01.07.2019

- RMRS Watches
- leather is more formal, then goes metal then fabric.
- Examples of watch band leathers:
  - Телячья кожа
  - Bison
  - Ящерица
  - Змея
  - Аллигатор
- Примеры металлического ремня:
  - Нержавеющая сталь
  - Титан
  - Алюминий
  - Родий
- Alongside, Insinuation, Jealousy, Intention
- Temp Score
- 33 стратегии войны
- 48 закон руководства
- Приказ красивыми словами
- Уж лучше один плохой генерал, чем два хороших. - Наполеон Бонапарт.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 79, DATE '2019-07-02', '# Day 79 — 02.07.2019

- Уэс Андерсон
- фильм "Она", Сцена Секса.
- фильм "Интерстеллар" — сцена осветила поле от пыли.
- фильм "Дюнкерк" — Сцена взрыва бомб бомбардировки.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 80, DATE '2019-07-05', '# Day 80 — 05.07.2019

- Армия Наполеона - Grand Armee. Причины.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 81, DATE '2019-07-07', '# Day 81 — 07.07.2019

- Фильм "Ничего хорошего в отеле Эль Рояль" (1969г):
  - Uncheived melody
  - Шеймас Макгарви - оператор
  - Дакота Джонсон
  - Крис Хэмсворт
- Камердинеры
- Менеджером
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 82, DATE '2019-07-10', '# Day 82 — 10.07.2019

- Принцип работы противозачаточных таблеток
- Собаки зализывают друг другу раны, чтобы стая была здоровой.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 83, DATE '2019-07-11', '# Day 83 — 11.07.2019

- Mustn''t vs don''t have to do
- Volatile, unpredictable, fickle
- It''s not the case
- to frighten
- vulnerable, helpless, defenseless, weak
- Obviously, clearly
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 85, DATE '2019-07-16', '# Day 85 — 16.07.2019

- Millenium Challenge 2002.
- JFCOM
- Импровизация "Гаральд"
- Про аутизм и необходимость распознать невербалику.
- Про эксперимент фильма и аутиста.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 86, DATE '2019-10-13', '# Day 86 — 13.10.2019

- Межхирургический и арабский разведотдел.
- История бутафорских самолётов и танков.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 87, DATE '2019-11-07', '# Day 87 — 07.11.2019

- Աշխարհը - никогда пернуть
- Ծրդրվել - գժվել սթրեսից
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 88, DATE '2019-11-13', '# Day 88 — 13.11.2019

- By the way - Кстати
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 89, DATE '2019-11-27', '# Day 89 — 27.11.2019

- Throughout - на протяжении
- Inevitably - неизбежно
- Elaborate - complicated, detailed
- To elaborate - разрабатывать
- Justify - обосновывать
- Confuse - запутывать
- скорее - rather
- offend - обижать, оскорблять (hurt)
- frustration - разочаровани�B�H��Yܙ]H4`t/�-�,4.�-t`�c�H�]�X]H4/�`�`t`�`�/�.�-t/t.4-B', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 90, DATE '2019-12-04', '# Day 90 — 04.12.2019

- Conceal - скрывать (hide)
- fascinating - очаровательный
- straightforward - простой
- subtle - тонкий, утонченный
- deny - отрицать (refuse)
- claim - запрос, требовани�B�H�\��[H4,�b�`�a�-t/t/tb�.B�H��Y\H4/t-t`t`�.�H[��HH4-�,4,�.4-4`�c��H�X�[H4`4/�-4/�,�/�.B�H[؈H4b4,4.t.�,�H[�[ZY][ۈH4`�`t`�`4,4b4-t/t.4-B�HX^Z[HH5i�hu�5l5n5���5hum���H��[HH5i5j�m5hui4`4-t.�c�H\�[H4/�/�,4`t/t/�`t`�c
^�\�
B�H\�]H4/�,�/�`4a�-t/K�', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 91, DATE '2019-12-05', '# Day 91 — 05.12.2019

- Как в СССР воровали мясп
- Помба = ''Ритм-вот''
- Французы ''как не заметип''
- Как взять бензин из канистры.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 92, DATE '2019-12-06', '# Day 92 — 06.12.2019

- Взять в аренду гораздо дешевле покупки.
- Мухаммед Али = Cassius Clay
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 93, DATE '2019-12-07', '# Day 93 — 07.12.2019

- Рубен Малая - Калининград
- Катализатор; противогаз
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 94, DATE '2019-12-08', '# Day 94 — 08.12.2019

- в лазат
- Epidemic sound
- Tribute
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 95, DATE '2019-12-09', '# Day 95 — 09.12.2019

- Martini
- Iconic
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 96, DATE '2019-12-10', '# Day 96 — 10.12.2019

- Ryan Hethington
- Luca Boggetto
- Arumpggak
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 97, DATE '2020-01-03', '# Day 97 — 03.01.2020

- Why are race cars right hand drive
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 98, DATE '2020-01-08', '# Day 98 — 08.01.2020

- Трюк IVI Networks со стартапами (не можешь победить, возглавь)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 99, DATE '2020-01-09', '# Day 99 — 09.01.2020

- Difference between introvert and extrovert narcissists (Childhood, ...)
- Almost all dictators are deep narcissists.
- We love people who share our ideas
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 100, DATE '2020-03-23', '# Day 100 — 23.03.2020

- Present perfect continuous for saying how long for something still happening.
- Have you ever been to California.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 101, DATE '2020-03-24', '# Day 101 — 24.03.2020

- Use past simple instead of present perfect when for things that are not recent or new — Mozart was a composer. He wrote more than 600 pieces of music.
- We use present perfect to give new information. But if we continue to talk about it we normally use past simple.
- Use past simple instead of pr. perfect when asking when or what time.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 102, DATE '2020-03-27', '# Day 102 — 27.03.2020

- What are you doing on Saturday morning (Not what do you do)
- Use pr. simple in the future meaning not with timetables or programmes.
- Use "will" when just decided.
- When I''ve phoned Kate, we can go out.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 103, DATE '2020-03-28', '# Day 103 — 28.03.2020

- Как усилить недруга (33 стр войны page 42)
- Чем полезен враг.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 104, DATE '2020-03-29', '# Day 104 — 29.03.2020

- Минусы коллегиального руководства (33 стр. войны page 43).
- Почему зеркало меняет право и лево, а не вверх и вниз.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 105, DATE '2020-04-01', '# Day 105 — 01.04.2020

- 5 категорий небесных тел — Луна, ... — Марс, ...
- Сфера Нептуна (лазурит)
- If I do vs If I did (Unit 38)
- If I were here
- I wish Anna were here
- If I had known
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 106, DATE '2020-04-02', '# Day 106 — 02.04.2020

- Архитектура Баланса.
- Регрессирование короткокорпусом может быть проблема диагностирования.
- Мне дали эти часы -> English
- Переведи "Вы едите много фруктов" / "Есть ли кто-то в этом районе" / "Бензин должен быть высокого качества" / "Гораздо интереснее" / "Говорят, ...": It is said,..
- have something done
- Переведи — "Они могли бы нас победить, но не победили" — "Он мог застрять в пробке" — "Когда я пришёл, машинистка печатала письма, которые я ей дал накануне." — "Я метал весь вечер вчера" (Don''t use have been)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 107, DATE '2020-04-04', '# Day 107 — 04.04.2020

- The first / the last / the only + to
- See do vs see doing
- Переведи — "Его спросили, где он живет" He was asked where he lived — "Что производится здесь" What has been produced here
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 108, DATE '2020-04-07', '# Day 108 — 07.04.2020

- Люди любящие контролировать ход и сплетничать.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 109, DATE '2020-04-08', '# Day 109 — 08.04.2020

- фильм Малена
- сцена с короткой стрижкой
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 110, DATE '2020-04-09', '# Day 110 — 09.04.2020

- A friend of mine.
- on my own = by myself = alone
- much/little for uncountable
- Both of + this/these
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 111, DATE '2020-04-10', '# Day 111 — 10.04.2020

- Will vs would
- So + adverb/adjective  Such + noun
- Коронавирус идея текущего барьера (не можешь победить - возглавь)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 112, DATE '2020-04-12', '# Day 112 — 12.04.2020

- Unless = except if
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 113, DATE '2020-04-13', '# Day 113 — 13.04.2020

- Танец пчёл как навигация
- Пчела-разведчик.
- Как пчёлы выбирают новый «дом»
- Негативный прожектор разведчиков. Удивительно как они с точностью дерева находят его в лесу.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 114, DATE '2020-04-18', '# Day 114 — 18.04.2020

- Переведи — Картины находятся на стенах — До сих пор мы видим одного красивого — Он продает больше чем все остальные
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 115, DATE '2020-04-19', '# Day 115 — 19.04.2020

- Don''t use pr. cont. with "need"
- Как правильно дезинфицировать (33 стр. 18 page 480)
- Пример Мейнерцхагена: 2 основные причины
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 116, DATE '2020-04-30', '# Day 116 — 30.04.2020

- Homo Sapiens раз вид — Homo neanderthalensis — Homo soloensis
- Линейная модель развития человека разумного ложная
- Это исключение, что у человека нет других видов.
- Почему человеческие дети рождаются «преждевременно», чем у других животных.
- В течение 2 млн лет человеческий мозг не мешал ему и он занимал среднюю нишу в природе. (Пример почему ели костный мозг)
- Вооруженные копья гораздо опаснее вооруженных волков.
- 30.000 лет назад в Непале были неандертальцы.
- Теория вытеснения и политкорректность.
- Огонь и камень (пример на Пежо)
- Как лгут злейшие мартышки
- Параллель между шаманами и юристами.
- Язык использовался не только для охоты, а для сплетен тоже, чтобы образовать социальные группы.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 117, DATE '2020-05-02', '# Day 117 — 02.05.2020

- Человек и в прошлом вредил природе пример Австралия, Новая Зеландия
- Чем опасны союзы (33 стр войны Page 535)
- Сила непредсказуемости (33 стр войны page 650)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 118, DATE '2020-05-03', '# Day 118 — 03.05.2020

- Важность дел а не слов (33 стр войны)
- Аграрная революция и его негативные последствия.
- Как пшеница «использовала» людей для эволюции.
- Как из коровы получали молоко
- Аграрная революция положительно повлияла на эволюцию коров как вид, но как индивидуальность они самые несчастные животные. Не то же самое происходит с человеком?
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 119, DATE '2020-05-05', '# Day 119 — 05.05.2020

- Камурании и вежливости козла виолончелист ("О пр. человека")
- Понятия "объективное", "субъективное", "интерсубъективное".
- Пример интерсубъективного понятия - Peugeot ("О пр. человека")
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 120, DATE '2020-05-06', '# Day 120 — 06.05.2020

- Вагоны и граффити ("Перемирие" page 69)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 121, DATE '2020-05-12', '# Day 121 — 12.05.2020

- Can vs have
- was able vs could
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 122, DATE '2020-05-13', '# Day 122 — 13.05.2020

- Описание ориентации словами ("Сила мягких речей" pg 44)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 123, DATE '2020-05-14', '# Day 123 — 14.05.2020

- Как все народы поверили в золото
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 124, DATE '2020-05-25', '# Day 124 — 25.05.2020

- Some vs Any (Том стр. 29)
- Myself/himself/yourself — -ся (занимался) — себя/собой (я недоволен собой) — сам/сами
- Самосбывающиеся предсказания (Self-fulfilling prophecy) Examples — Sport ("Гении и аутсайдеры" — Education system часть 1) — Пример высокого дерева и леса.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 125, DATE '2020-05-28', '# Day 125 — 28.05.2020

- Как бороться со спидраном
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 126, DATE '2020-06-15', '# Day 126 — 15.06.2020

- I''m regretting
- Pr. continuous on anecdotes
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 127, DATE '2020-06-22', '# Day 127 — 22.06.2020

- Will you stay vs will you be staying
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 128, DATE '2020-06-24', '# Day 128 — 24.06.2020

- Parking slots and temperature
- Free parking is unfair
- Creating larger parking slots force buildings to be farther from each other thereby encouraging people to use cars.
- Rebound effect
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 129, DATE '2020-06-30', '# Day 129 — 30.06.2020

- Rolls Royce
- Каллиграфия, каллиграфутуризм
- Set by; North Pole (J. Clarkson)
- Волюнтаризм
- анализ Ford Pinto
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 130, DATE '2020-07-02', '# Day 130 — 02.07.2020

- Причины появления грязи — Открытый грунт (нужно укласть газоном), или решёткой под деревья. — Высокие тротуары (должны быть наоборот)
- Отсутствие дренажных ям
- Townhouse
- Скорость выше 85-90 км/ч в городе препятствует увеличению пропускной способности из-за увеличения расстояния между машинами.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 131, DATE '2020-07-03', '# Day 131 — 03.07.2020

- Проблема рамп-металлоискателей
- Маршрут может создать борьбу у рамп, где большая очередь
- Проблема эвакуации
- Rooftop gardens (New York; Singapore)
- в заборе сужают тротуары
- Rooftop advantages — Safe as no cars — Safe as no strangers
- Водонепроницаемый асфальт
- Ливневые горшки
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 132, DATE '2020-07-21', '# Day 132 — 21.07.2020

- Vision Zero (Стокгольм)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 133, DATE '2021-03-01', '# Day 133 — 01.03.2021

- Installation art
- The Weather project in Tate Modern
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 134, DATE '2021-06-27', '# Day 134 — 27.06.2021

- Альвео доселе
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 135, DATE '2021-07-15', '# Day 135 — 15.07.2021

- Мы говорим «Я слишком занят, чтобы разобраться в себе», гендиректорам компаний, которые профессионально занимаются этим. И как только они могут посчитать как насколько больше стал мы сами, мы станем предметом манипуляций.
- If I had asked people what they wanted, they would have said faster horses.
- В начале 1920-х бензин был лишь чистящим средством и сейчас никто бы не подумал использовать Varnish как топливо.
- Ручной стартер убил
- Электромобили заряжались кнопкой
- Detroit Electric — которую ночь добрая машина, помня, зарядил и утром поставил возле дома.
- Ford T ездил на спирте.
- До керосина люди сжигали касторовый жир для освещения.
- При сильном ветре при кипячении нефти получается керосин, а при слабом - бензин.
- После сухого закона все перешли на бензин (сухой закон подружился Рокфеллер)
- Бензин со свинцом
- Люк выяснилось, что свинцо стало дольше только в 20-ом веке.
- Парадокс корабля Тесея
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 136, DATE '2021-07-18', '# Day 136 — 18.07.2021

- Терренс Малик ("Древо жизни")
- пр. Джандры Лоулесс
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 137, DATE '2021-07-19', '# Day 137 — 19.07.2021

- Парадокс — + 2 продал за 5000$ — - Куплю за 4000$ — - Ок
- При переговорах (Гэвин Кеннеди) — Не женитесь, а требуйте — Не торговать сам с собой
- При переговорах нельзя сделать добровольные уступки, оправдывая это.
- Чтобы ослабить сопротивление (Более умиротворит позицию)
- После переговоров не тронулся с места
- Не обращать внимание на роспись кабинета при переговорах
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 138, DATE '2021-08-19', '# Day 138 — 19.08.2021

- Скевоморфизм
- iOS 2013 flat design — Before them Microsoft — Before them Malevich
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 139, DATE '2021-11-16', '# Day 139 — 16.11.2021

- feasible - пригодный
- decay - разлагаться
- omit - пропускать
- legislation - законодательство
- cohesion - связь
- fallacy - заблуждение
- Smoking gun
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 140, DATE '2022-07-03', '# Day 140 — 03.07.2022

- TBC was a partner not teambuilding
- Bottle of water vs a vase
- Cars in arabian countries are white
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 141, DATE '2022-07-04', '# Day 141 — 04.07.2022

- Пахнет Леонардо чтобы маскироваться
- Фрукты и ягоды специально выглядят так ярко, чтобы приманивать животных
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 142, DATE '2022-07-05', '# Day 142 — 05.07.2022

- Подземные переходы неудобные для мам, бедных, ...
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 143, DATE '2022-09-26', '# Day 143 — 26.09.2022

- Pneumatic waste collection
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 144, DATE '2022-09-28', '# Day 144 — 28.09.2022

- Work your tail off
- Терренс Малик перенял работы у Мартина Хайдеггера.
- дизайн - способность в человеке зажечь вопрос о бытии
- Хороший вкус - выраженный в форме ум
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 145, DATE '2022-12-05', '# Day 145 — 05.12.2022

- Չնչացնել հիթաթափել - փոշի/փոշ. հավաք.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 146, DATE '2022-12-12', '# Day 146 — 12.12.2022

- Distinctive style; carefree
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 147, DATE '2023-01-01', '# Day 147 — 01.01.2023

- This can be contrasted with
- It''s not unheard of
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 148, DATE '2023-03-01', '# Day 148 — 01.03.2023

- It''s more important to do big things well then small things perfectly
- man of purpose
- Timeless elegance
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 149, DATE '2023-03-05', '# Day 149 — 05.03.2023

- Керес, Сирон
- Collective - Asador, Minas, Black Angus, Limone
- Gyros
- Bread street kitchen & Bar
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 150, DATE '2023-03-27', '# Day 150 — 27.03.2023

- Earth is smother than any cue ball is ever machined
- Land Bridge Theory
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 151, DATE '2023-06-12', '# Day 151 — 12.06.2023

- Panamax and NeoPanamax
- Панамский канал замок проходит воду чтобы люди и грузы не просачивались дух океан.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 152, DATE '2023-07-02', '# Day 152 — 02.07.2023

- Ray Dalio — How do you know that you are not the wrong one? — Don''t let your need to be right to win to ''What''s right''. — Pain + reflection = progress
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 153, DATE '2023-10-30', '# Day 153 — 30.10.2023

- Alfa Romeo Silverstone
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 154, DATE '2023-11-02', '# Day 154 — 02.11.2023

- Ex-dividend
- Record date
- 1966 Ken Miles / Bruce McLaren
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 155, DATE '2023-12-06', '# Day 155 — 06.12.2023

- 3 theories why the democratic future will be without war — Democratic peace — Economic interdependency — Higher institutions
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 156, DATE '2023-12-19', '# Day 156 — 19.12.2023

- Գինու աշխարհ
- Kimono Geisha
- Kioto
- Why Geishas used white makeup
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 157, DATE '2024-01-12', '# Day 157 — 12.01.2024

- Double Brokering
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 158, DATE '2024-01-28', '# Day 158 — 28.01.2024

- Stanley Cup
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 159, DATE '2024-01-31', '# Day 159 — 31.01.2024

- Почему мусульманские страны беднее — Отсутствие банков долгое время (%) — В 19в. шиит. исламский стал все под запретом
- Фр. грам. и Турции 1960г = 1850г Голландия
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 160, DATE '2024-02-06', '# Day 160 — 06.02.2024

- Mozzarella lives 10-12 days
- Brie cheese
- Parmesan is fake
- DOP - Denominazione di Origine Protetta
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 161, DATE '2024-03-11', '# Day 161 — 11.03.2024

- History of Panama and USA
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 162, DATE '2024-03-12', '# Day 162 — 12.03.2024

- Хамон Серрано - из обычных белых свиней
- Хамон Иберико - из чёрных свиней с черными копытами
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 163, DATE '2024-03-13', '# Day 163 — 13.03.2024

- Macartney mission to China
- Son of heaven
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 164, DATE '2024-03-15', '# Day 164 — 15.03.2024

- Opium Wars — 1839-1842 — 1856-1860
- Итог 1ой опиумной войны — Финансовая компенсация — Открытие больше портов для Брит. торговли — Гонконг переходит к Англии — Китай и Англия равны — Экстерриториальность для британцев — Разрешение на импорт опиума
- why deflation is bad
- Nylon Dupont
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 165, DATE '2024-03-21', '# Day 165 — 21.03.2024

- Meyer Lansky
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 166, DATE '2024-03-22', '# Day 166 — 22.03.2024

- The "Undoing Project" tells about Daniel Kahneman who is the author of "Think fast and slow"
- Halo effect
- Yellow David star
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 167, DATE '2024-03-23', '# Day 167 — 23.03.2024

- Gulf Countries
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 168, DATE '2024-03-28', '# Day 168 — 28.03.2024

- Paolo Genovese
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 169, DATE '2024-04-11', '# Day 169 — 11.04.2024

- Arable land
- Topsoil
- Vertical farming
- Duopoly
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 170, DATE '2024-04-20', '# Day 170 — 20.04.2024

- Twitter community notes
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 171, DATE '2024-04-21', '# Day 171 — 21.04.2024

- law of small numbers
- Anchor effect (Wheel of fortune)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 172, DATE '2024-04-27', '# Day 172 — 27.04.2024

- Cognitive fallacies — Conjunction fallacy (Linda problem) — Anchoring Bias — The Gambler''s fallacy
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 173, DATE '2024-05-06', '# Day 173 — 06.05.2024

- Behavioral Finance
- Narrow framing
- Hindsight
- Prospect theory
- Geographical time scale — Eon — Era — Period — Epoch
- Eons — Hadean (~4.5 bln, mainly lavas, Moon formed from asteroid collision) — Archean (~1.5 bln, only unicellular life) — Proterozoic (~2 bln, mass extinction of anaerobes as oxygen rose) — Phanerozoic (visible life, The Great Dying because of Siberia volcanoes, 90% of species died)
- Mesozoic era (dinosaurs) ~250 mln years ago
- Mammals appeared. Ended with asteroid clash.
- Now we live in Holocene Epoch
- Homo appeared ~2.5 mln years ago
- Homo sapiens ~300,000 years ago
- Holocene started ~12,000 years ago
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 174, DATE '2025-02-06', '# Day 174 — 06.02.2025

- Lithium battery — anode (Graphite) — separator — cathode (Nickel, Cobalt, Manganese) — electrolyte (Lithium)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 175, DATE '2025-02-14', '# Day 175 — 14.02.2025

- Duration vs neglect
- Peak-end rule
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 176, DATE '2025-07-28', '# Day 176 — 28.07.2025

- Availability heuristic
- Aldous Huxley - The Brave New World
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 177, DATE '2025-08-02', '# Day 177 — 02.08.2025

- Турки
- Иудейская плита
- Самалийская плита
- Под Ял-мекдский призыв
- Первые корейцы появились в Континентальном Китае ~2500 году.
- Карлик парализует насекомых и из-за этого гусеница защищается
- Pup: хрен из Москвы

---

# Day 226 — 02.08.2025

- Piedmont - Turin
- Белый трюфель - в основном в Пьемонте не культивируется искусственно, но чёрный трюфель можно вырастить
- Салями в основном из свинины
- Умбрия - Перуджа
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 178, DATE '2024-08-06', '# Day 178 — 06.08.2024

- Pop-up restaurant
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 179, DATE '2024-08-09', '# Day 179 — 09.08.2024

- Île de la Cité - Island of the city
- 52 B.C. before Romans conquered this was Paris
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 180, DATE '2024-09-26', '# Day 180 — 26.09.2024

- Lagoon, Bay, Atoll
- It is predicted that in 100 years Maldives will not exist and will be under the ocean
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 181, DATE '2024-09-28', '# Day 181 — 28.09.2024

- Один разряд молнии хватит чтобы 5 месяцев снабжать частный дом
- Мусор на американской ферме
- Лопасти от ветряков - стеклопластик
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 182, DATE '2024-10-01', '# Day 182 — 01.10.2024

- Sangiovese - Italian indigenous wine grape variety
- Tannins - substances in grape skins and seeds that create drying, rubbing sensation on tongue
- In Bordeaux because of the Atlantic climate (unpredictable) wines are mostly blended from different varieties
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 183, DATE '2024-10-04', '# Day 183 — 04.10.2024

- Orion glasses
- Monaco facts — Second smallest country — Constitutional monarchy — Ruled by Grimaldi family since 1297 — Monegasques are not allowed to gamble in the casino — Defended by France and has no military force (80 soldiers)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 184, DATE '2024-10-07', '# Day 184 — 07.10.2024

- Inditex — Zara — Bershka — Pull & Bear — Oysho — Stradivarius — Massimo Dutti
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 185, DATE '2025-10-08', '# Day 185 — 08.10.2025

- Bitcoin requires maintenance but the gold does not
- Франк КФА (franc CFA)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 186, DATE '2025-10-17', '# Day 186 — 17.10.2025

- Самолеты летают на высоте 10км
- В самолёте предство низкое давление и люди чаще пускают газы
- Дверных замков нет, так как нельзя открыть дверь
- Зачем ремни азарожим
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 187, DATE '2025-10-20', '# Day 187 — 20.10.2025

- Вся жизнь земли основана на «левой» аминокислотах
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 188, DATE '2025-01-06', '# Day 188 — 06.01.2025

- Торф -> Антрацитовый уголь -> Каменный уголь -> антрацит
- Горючие как ценная реакция
- Зачем нужен нагрев
- why metal feels cooler than wood

---

# Day 189 — 06.01.2025

- Campania, Lazio - Italian Regions
- Buffalo mozzarella
- San Marzano tomato
- Camorra
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 190, DATE '2025-01-07', '# Day 190 — 07.01.2025

- В 1913 рубль был самой стабильной валютой в мире благодаря привязке к золоту
- Первый дизельный теплоход в России
- Первый пассажирский самолёт "Илья Муромец" - Игорь Сикорский, который сбежал после революции в Америку и построил вертолётную индустрию
- 70% призывников грамотные
- Сергей Витте — Денежная реформа — Транссибирская магистраль
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 191, DATE '2025-01-11', '# Day 191 — 11.01.2025

- Смутное время после опричнины Ивана Грозного и голода 1601-1603 годов.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 192, DATE '2025-01-12', '# Day 192 — 12.01.2025

- Ian Schrager - hotelier and founder of Studio 54
- Harley J. Earl - designer in G.M., founder of tail fins
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 193, DATE '2025-01-17', '# Day 193 — 17.01.2025

- Centrifugation
- Rigatoni Amatriciana
- Le Quattro Paste di Roma
- Pecora-овца/сыр Pecorino-овечий сыр
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 194, DATE '2025-01-19', '# Day 194 — 19.01.2025

- Wes Anderson - Director of "Hotel Grand Budapest"
- Daimler

---

# Day 237 — 19.01.2025

- Human inventions timeline:
  - 300,000 BCE - homo sapiens
  - 65,000 BCE - bone tools
  - 9000 BCE - cattle domestication
  - 9000 BCE - wheat in Indian valley
  - 7500 BCE - wheat in Mesopotamia
  - 5000 BCE - rice cultivation
  - 4000 BCE - potters wheel
  - 4000 BCE - horse domestication
  - 3500 BCE - wheel in Sumer
  - 3200 BCE - sail in Egypt
  - 1500 BCE - Phoenician alphabet
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 195, DATE '2025-01-21', '# Day 195 — 21.01.2025

- Тордесильясский договор
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 196, DATE '2025-01-23', '# Day 196 — 23.01.2025

- Why was the Aegean sea coast the most important area for the Greek civilization
- This cold geographical features influenced the political fragmentation of Greece and it helped them from Persia
- What was the significance of the numerous islands of the Aegean sea for trade and travel
- What was the role of olive oils in the daily life and maritime expansion of the Greeks
- Wine and Malaria
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 197, DATE '2025-01-25', '# Day 197 — 25.01.2025

- Pro-latin - серия
- Hoplite revolution
- Toraks
- В древности главным оружие - копьё
- Пилум
- One death is a tragedy, a million is a statistics. Stalin.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 198, DATE '2025-01-26', '# Day 198 — 26.01.2025

- После Бронзового коллапса пропала: — Центральная власть — Государство — Письменность
- В Египте был самый плодородный
- Нил предсказуемая река
- Бронза = олово + медь
- Греки взяли алфавит из Финикийского
- Греки имели около 700 стран, дошли до Крыма
- Never Complain, never explain R. Greene
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 199, DATE '2025-01-28', '# Day 199 — 28.01.2025

- Архаические уроки из-за местных причин можно было обмануть и это одна из причин почему там была философия, демократия и спорт
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 200, DATE '2025-01-29', '# Day 200 — 29.01.2025

- 518 BCE in Persia Satrapies were introduced and it made Ionic Greeks to make a rebellion
- Было 5 Этапов Греко-Персидских войн — Восстание Милета — I война заканчивается в Марафоне, где греки застали Персов врасплох — Ксеркс и Арий - это персидские имена — II война (Ксеркс) - здесь и произошла знаменитая битва 300 спартанцев, победили Афины
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 201, DATE '2025-01-31', '# Day 201 — 31.01.2025

- Делосский союз - союз под руководством Афин с казной в Делосе. Это был морской союз
- Персия помогла Пелопонесскому союзу
- Афины убедили союзников перевести казну в Парфенон и после начали силой противостоять восстаниям
- Фимистокол был изгнан в Персию и стал советником Ксеркса
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 202, DATE '2025-02-01', '# Day 202 — 01.02.2025

- French Polynesia and Kiribati are not in the same -10 timezone but Kiribati, which is between them, is +14
- Poly Nesie = Poly(gr. many) + Nesos(gr. island). Same way Indonesia
- Персы делали деньги Спарте во время Пелопонесских войн, а после начали давить Афинян. Но в конце концов это было неверно, так как постоянные войны лишали военную мощь греков
- Narrative fallacy is because of the way our brain works to memorize — it uses logic to remember
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 203, DATE '2025-02-02', '# Day 203 — 02.02.2025

- С Александром Великим в поход шли ученые, геологи, философы. Это были тексты научной экспедицией.
- В Индии тогда тоже была формальная важная логика и математика.
- Перс - это эллинизированное имя, они себя называли Чичи
- Те народы, которые узнали греков из Италии (западной части) называют их Греки (из-за этого рум), а восточные народы как мы — из-за ион из Ионии
- Гимнастика - БЦБ
- От Италии до Индии говорили греческий
- Греки научили индусов скульптуре Будды и как референс взяли Аполлона, у кого кудрявые волосы
- В Александрии создаётся библиотека, где собираются все изданные книги со всего мира
- Из Палестины евреи идут в Египет и переводят Ветхий Завет
- Зачем у африканцев такие ноздри и губы
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 205, DATE '2025-02-11', '# Day 205 — 11.02.2025

- Эвер по аккадски - бык и это была луна для Н и рисовали так быка V
- Междуречье и Всемирный потоп
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 206, DATE '2025-02-12', '# Day 206 — 12.02.2025

- Легенда о Гильгамеше - самая старая книга и она была написана на глине
- Изобретение колеса - 3500 лет до н.э.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 207, DATE '2025-02-13', '# Day 207 — 13.02.2025

- Изобретение рабства была гуманистическим
- Древнейшие люди также знали, что нужно выйти замуж с другими племенами
- Чатал-Гуюк
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 208, DATE '2025-02-22', '# Day 208 — 22.02.2025

- Миланская кухня — Мало помидоров — Нет пасты — и сливочное масло
- Ломбардия - регион Милана
- Duomo di Milano
- Италия главный поставщик риса в Европу
- Spear Speakeasy
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 209, DATE '2025-03-08', '# Day 209 — 08.03.2025

- Pomo d''oro = золотое яблоко. Помидоры начали в Италии в XVI веке
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 210, DATE '2025-03-09', '# Day 210 — 09.03.2025

- From Italy to Sicily
- Arabs brought spaghetti to Italy
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 211, DATE '2025-03-11', '# Day 211 — 11.03.2025

- Аккадская империя - первая империя
- Саргон
- Gobal Hoyuk lacked key features to be called a civilization like no centralized government (8500 BCE-5700 BCE)
- Аккады - семиты
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 212, DATE '2025-03-15', '# Day 212 — 15.03.2025

- Законы Хамурапи хранятся в Лувре
- Плодородный полумесяц
- Причины возникновения письменности
- Достижения шумерской цивилизации — Письменность — Колесо (~3500 до н. эры) — Ирригационная система — Первые своды законов — Календарь и система счисления 60 — Города-государства — Зиккураты
- Шумерские города — Ур — Урук — Лагаш — Киш
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 213, DATE '2025-03-18', '# Day 213 — 18.03.2025

- Кипр = медь (cuprium)
- Троя - перевалочный пункт олова
- В конце Бронзового века средний рост египтянина 178-180 см
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 214, DATE '2025-03-22', '# Day 214 — 22.03.2025

- Гильгамеш был написан на глине
- Основные причины Бронзового коллапса — Миграция — Изобретение кулака — Захват низавий олова — Народы моря идут из центра
- 1258г до н.э. Египет и Хеты подписали первый в мире Мировой договор
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 215, DATE '2025-03-24', '# Day 215 — 24.03.2025

- 11.000 лет до н.э. заканчивается последний ледниковый период и он называется плодородный полумесяц. Также Нил становится более предсказуемым.
- ~7500 лет до н.э. Чатал-Хююк
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 216, DATE '2025-03-06', '# Day 216 — 06.03.2025

- Deir-el-Bahari
- Matrilinearity
- В древнем Египте наследственность считали по материнской линии
- Nile insemination by Pharaoh
- Египетские фараоны страдали от боли зубов
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 217, DATE '2025-04-29', '# Day 217 — 29.04.2025

- Put the i''s, cross the t''s
- I''ll make it up to you
- To be on high
- You''ve got the floor now
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 218, DATE '2025-05-02', '# Day 218 — 02.05.2025

- Put it on ice
- What you''re up to
- to go berserk
- It doesn''t ring a bell
- Something has come up
- Working our way forward
- I''ll have to be you
- He''s fair game
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 219, DATE '2025-05-05', '# Day 219 — 05.05.2025

- Is everything to your liking?
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 220, DATE '2025-05-08', '# Day 220 — 08.05.2025

- American press vs french press
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 221, DATE '2025-05-22', '# Day 221 — 22.05.2025

- Tiber - the main river of Rome
- Trastevere = Across the Tiber
- Piazza del Popolo = People''s square
- It is one of the gates of the city
- Piazza Navona
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 222, DATE '2025-06-21', '# Day 222 — 21.06.2025

- Ливан - ближневосточная Швейцария
- Бейрут - ближневосточный Париж
- Франция получила мандат на Ливан и Сирию
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 223, DATE '2025-06-22', '# Day 223 — 22.06.2025

- What is 1 second
- How Cesium-based atomic clock works
- U-235 isotope must be >80% for atomic bomb
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 224, DATE '2025-06-30', '# Day 224 — 30.06.2025

- Relay for computers
- Vacuum tube as an electrical switch
- ENIAC 1945
- Silicon is a semiconductor and transistors are based on it and they replaced vacuum tubes.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 225, DATE '2025-07-01', '# Day 225 — 01.07.2025

- Рикотта - сывороточный сыр, в основном остается после приготовления моцареллы
- Буйволы в Италии попали с арабами
- Mozzarella = "Mozarre" = "cut off" (~motion)
- Emilia Romagna
- Parmigiano Reggiano
- Prosciutto di Parma (pork)
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 227, DATE '2025-08-04', '# Day 227 — 04.08.2025

- Похожий синдром
- Мудрость в Японии
- Bitter honey on Sardinia
- Casu Marzu
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 228, DATE '2025-08-10', '# Day 228 — 10.08.2025

- ASML spinned off from Philips which is a Dutch company
- Philips was a big investor of TSMC
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 229, DATE '2025-08-11', '# Day 229 — 11.08.2025

- Пьемонт (Турин) — Турин - несколько лет столица Италии — Битва апельсинов — Белый трюфель — Сливочное масло — Slow food — Бичерин — FIAT
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 230, DATE '2025-09-02', '# Day 230 — 02.09.2025

- AT&T sparked the transistors revolution in 1947 at Bell labs as they wanted an amplifier
- Invention of IC
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 231, DATE '2025-09-25', '# Day 231 — 25.09.2025

- Cartagena - Новый Карфаген. Был построен Гамилькар Баркой
- Квактюра
- Пунические войны — I - 264-241 BC — II - 218-201 BC — III - 148-146 BC
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 232, DATE '2025-10-02', '# Day 232 — 02.10.2025

- Gallus (latin) = աքաղաղ (петух)
- Provincia nostra = наша провинция -> Provence (Франция)
- Галлия в штанах
- In every generation one must see oneself as if he personally came out of Egypt
- What Jews do during Passover night
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 234, DATE '2025-08-09', '# Day 234 — 09.08.2025

- Humans are naturally good at remembering stories. That''s why writing''s most biggest advantage is recording lists and numbers and not sharing stories.
- Three realities — Objective — Subjective — Intersubjective
- Netherlands = Low Lands — Biggest artificial island — Rhine river is used for cargo vessels — Frankfurt am Main has a cargo port on river — Rembrandt is Dutch — New York was initially called New Amsterdam
- Poke is Hawaiian
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 235, DATE '2025-10-18', '# Day 235 — 18.10.2025

- По иудейской традиции только один храм может считаться Домом Бога, где можно делать жертвоприношения, но молиться можно в синагоге.
- В этом храме Иисус разрушил все лавки торговцев.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 236, DATE '2025-10-26', '# Day 236 — 26.10.2025

- V тысячелетие у людей впервые появляются излишки продовольствия, что приводит к разделению труда.
- Примерно в это же время люди научились приготовить посуду из глины.
- Неолит начинается примерно -12.000 года из-за потепления
- Письмо появилось в конце IV тысячелетия до н.э. в Месопотамии и в Египте
- Лошадь была приручена в 3500 г. до н.э.
', now(), now());

INSERT INTO notes ("Id","Book","DayNumber","EntryDate","Content","CreatedAt","UpdatedAt")
VALUES (gen_random_uuid(), 'green', 238, DATE '2026-06-15', '# Day 238 — 15.06.2026

- Radiation when flying: at cruising altitude (~11 km) the thinner atmosphere blocks less cosmic radiation, so dose rate rises. A transatlantic flight ≈ 0.02–0.05 mSv (about a chest X-ray). Negligible for occasional travelers; adds up for aircrew and frequent flyers.
', now(), now());

COMMIT;

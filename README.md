# Web38
Багатопоточний посередник між 1С 8.3 та сайтом наприклад

Принцип роботи

Відкриває порт наприклад 5656 для прийому вхідних підключень по протоколу TCP

Одночасно запускає декілька робочих потоків, які підключаються до 1С 8.3 через COM


Клієнт пересилає запит у форматі ХМЛ. В запиті вказується яку функцію потрібно викликати в модулі
зовнішнього з'єднання та з якими параметрами. Результат функції також у форматі ХМЛ пересилаються 
клієнту.


Детальніше в проекті Web38Site

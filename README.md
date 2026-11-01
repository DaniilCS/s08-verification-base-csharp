# S08. Сборка, тесты и стенд проверок — C#/.NET

На занятии работаем со своей копией небольшого учебного API: выполняем локальные проверки, запускаем GitHub Actions, наблюдаем падение теста и исправление, собираем контейнер и поднимаем Compose-стенд. После пары переносим освоенные файлы в собственный командный продукт.

## 1. Что подготовить

Нужны Git, .NET SDK 10.0.401 (либо более поздний patch в SDK feature band 10.0.4xx), аккаунт GitHub с правом записи в свою учебную копию и Docker с Linux-контейнерами и Compose. На Windows показ рассчитан на VS Code, PowerShell и Docker Desktop в режиме Linux containers. Установка Docker и загрузка больших образов не входят в 80 минут; проверьте среду заранее по инструкции преподавателя. Docker Desktop должен быть запущен.

```powershell
git --version
dotnet --version
docker version
docker compose version
```

У `docker version` должны отвечать Client и Server. Одного установленного CLI недостаточно. Установленный SDK выбирается через global.json; сообщение «A compatible .NET SDK was not found» означает, что нужен совместимый SDK, а не только runtime.

Официальная установка: [Docker Desktop для Windows](https://docs.docker.com/desktop/setup/install/windows-install/). [Настройка WSL](https://docs.docker.com/desktop/features/wsl/) выполняется при необходимости до занятия, а не в середине демонстрации.

## 2. Своя копия на GitHub

Если преподаватель выдал ссылку на GitHub, откройте ее, нажмите Fork и создайте копию в своем аккаунте. В своей копии откройте Actions; если GitHub предлагает разрешить workflows, подтвердите запуск только для полученного учебного кода. Во вкладке Code → HTTPS скопируйте адрес именно своей копии.

Из родительского каталога, где еще нет s08-demo:

```powershell
$repoUrl = Read-Host "Вставьте HTTPS-адрес своей учебной копии на GitHub"
git clone $repoUrl s08-demo
cd s08-demo
code .
```

Если получен архив, откройте в VS Code папку s08-verification-base-csharp и создайте новый пустой репозиторий GitHub. Для первой отправки из этой папки:

```powershell
git init -b main
git add .
git status
git commit -m "Initial S08 scaffold"
$repoUrl = Read-Host "Вставьте HTTPS-адрес своего пустого репозитория GitHub"
git remote add origin $repoUrl
git push -u origin main
```

Если Git просит имя и email, задайте свои `git config user.name` и `git config user.email` для этой копии, затем повторите commit. Не создавайте второй origin, если репозиторий уже клонирован. Все последующие команды выполняются из корня объекта, где лежат Scaffold.slnx, Dockerfile и compose.yaml.

## 3. Локальная сборка и тесты

```powershell
dotnet restore --locked-mode
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --no-restore --logger "trx;LogFileName=s08-tests.trx" --results-directory TestResults
```

Ожидаются **8 пройденных тестов, 0 ошибок и 0 пропусков**. TRX появляется в TestResults. Release — выбранная конфигурация сборки; --no-build допустим только после сборки актуального кода в той же конфигурации. После изменения исходников сначала повторите build.

API для тестов отдельно запускать не требуется. Каждый тестовый случай получает собственный сервер и хранилище.

Для ручного запуска в первом терминале:

```powershell
dotnet run --project src/SampleApi --configuration Release --no-build --urls http://localhost:8080
```

Второй терминал:

```powershell
Invoke-RestMethod http://localhost:8080/health
Invoke-RestMethod http://localhost:8080/info
```

Ожидание: status=ok, label=s08-local. Либо откройте requests.http и нажмите Send Request над запросами 0 и 1. Если надписи нет, установите REST Client (`humao.rest-client`) через Ctrl+Shift+X и заново откройте файл.

Перед изменениями кода и повторной сборкой на Windows остановите ручной API в первом терминале через Ctrl+C. Перед контейнером также освободите порт 8080.

## 4. Зеленый CI и его свидетельство

В своей копии GitHub откройте Actions → S08 Build and test. Первый push запускает проверку автоматически. После Fork можно запустить вручную: Run workflow → выбрать main → Run workflow. Если workflow еще не виден, проверьте наличие ci.yml в .github/workflows на default branch и разрешение Actions для репозитория.

Откройте конкретный прогон, затем job build-and-test. Найдите шаги Checkout, Setup .NET, Record checked version, Restore, Build, Test, Publish application, Upload evidence. В Test должно быть восемь пройденных тестов.

Вернитесь на страницу прогона. Внизу, в Artifacts, скачайте архив с именем s08-evidence-<commit>-<attempt>. Внутри:

- Evidence/run-info.txt — фактически проверенный commit, событие, SDK и номер прогона;
- Evidence/dotnet-info.txt — сведения о среде .NET;
- TestResults — отчет тестов;
- Publish — опубликованные файлы приложения, если тесты прошли.

Артефакт хранится 30 дней или в пределах политики организации. Для последующей сдачи сохраните нужные отчеты в принятом месте проекта вместе со ссылкой на прогон и проверенным commit. Ссылка без доступного результата не заменяет свидетельство.

В pull request может проверяться служебный merge commit. Для точной версии смотрите checked_commit в run-info.txt; не подменяйте его произвольно выбранным hash ветки. Красный тест не отменяет загрузку уже созданных отчетов: Upload evidence имеет if: always(). При сбое до запуска тестов TRX может отсутствовать — это не означает ноль найденных нарушений.

## 5. Эксперимент «красный тест → диагноз → исправление»

Убедитесь, что ручной API остановлен. Создайте учебную ветку:

```powershell
git switch -c s08-experiment
```

Откройте tests/SampleApi.Tests/SmokeTests.cs. В методе Health_Returns_Ok найдите строку после S08_BREAK_POINT:

```csharp
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
```

Замените **только в этом методе** OK на NotFound, сохраните файл. Приложение не меняли; теперь тест ожидает неверный результат.

```powershell
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --no-restore
```

Ожидаются 7 пройденных и 1 упавший: Expected NotFound, Actual OK. Это намеренная учебная ошибка теста. В реальной диагностике выясняют, где причина: в ожидании, коде или окружении; цвет сам по себе ответа не дает.

```powershell
git add tests/SampleApi.Tests/SmokeTests.cs
git commit -m "S08 experiment: incorrect health expectation"
git push -u origin s08-experiment
```

В Actions откройте прогон именно этой ветки и этого commit. Красным должен стать шаг Test. Откройте журнал и найдите ожидаемый и фактический статусы.

Верните NotFound обратно в OK, сохраните. **git restore после красного commit здесь не исправит ошибку: она уже содержится в HEAD.** Исправьте строку явно.

```powershell
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build --no-restore
git add tests/SampleApi.Tests/SmokeTests.cs
git commit -m "S08 experiment: restore correct health expectation"
git push
```

Новый прогон должен быть зеленым, восемь тестов проходят. Старый красный прогон сохраняет результат старой версии. До контейнерного блока верните именно корректную строку.

## 6. Образ и отдельный контейнер

Остановите ручной API и прежний контейнер, если они занимают 8080. Docker Desktop должен работать в режиме Linux-контейнеров.

```powershell
docker build -t s08-demo:local .
docker run --rm --name s08-single -p 127.0.0.1:8080:8080 s08-demo:local
```

Последняя команда занимает терминал и показывает журнал приложения. Во втором терминале:

```powershell
Invoke-RestMethod http://localhost:8080/health
Invoke-RestMethod http://localhost:8080/info
docker ps
```

Ожидаются status=ok и label=s08-local; в docker ps виден s08-single. В записи порта слева указан порт компьютера, справа — порт приложения в контейнере. EXPOSE сам порт на компьютере не публикует.

Остановите контейнер из второго терминала:

```powershell
docker stop s08-single
```

Из-за --rm контейнер удалится после остановки; образ останется. docker run не выполняет тесты: он запускает уже собранное приложение. В Dockerfile тоже только публикация приложения; тесты выполнялись отдельно.

## 7. Compose-стенд и реальная настройка

```powershell
Copy-Item .env.example .env
docker compose config --quiet
docker compose up --build --wait --wait-timeout 120
docker compose ps
Invoke-RestMethod http://localhost:8080/health
Invoke-RestMethod http://localhost:8080/info
```

Команда --wait оставляет стенд работать в фоне и ожидает готовность. После успешного запуска docker compose ps показывает healthy; /info возвращает label=s08-compose. Во время первой загрузки образов/сборки нужно дождаться завершения; таймаут ожидания готовности не ускоряет загрузку.

Здесь APP_LABEL — **публичная настройка, не секрет**. Откройте .env, замените значение на APP_LABEL=s08-my-stand. Затем:

```powershell
docker compose up --build --wait --wait-timeout 120
Invoke-RestMethod http://localhost:8080/info
```

Ожидание: label=s08-my-stand. Compose передает значение в процесс API. Сам `dotnet run` файл .env автоматически не читает. Переменные PowerShell APP_LABEL/APP_PORT, если заданы, имеют приоритет над .env; фактическую конфигурацию видно через docker compose config.

```powershell
docker compose logs --tail 50
docker compose exec api dotnet SampleApi.dll --healthcheck
docker compose down
```

Самопроверка возвращает код 0, если /health доступен. Ее используют healthcheck Compose и дополнительная приемка. Образу не нужен curl. После down стенд остановлен и удален, образы остаются. Данные API только в памяти и при пересоздании контейнера исчезают; база данных, тома и внешние службы в этом примере не используются.

## 8. Конфигурация и секреты

Учебному API не нужны действующие секреты. APP_LABEL и APP_PORT не объявляются секретами. Пример .env.example хранится в Git; локальный .env исключен из Git и Docker-контекста.

После создания .env в Git-копии проверьте:

```powershell
git check-ignore -v .env
git ls-files .env
```

Первая команда показывает правило исключения. Вторая не должна вывести .env. Если файл уже отслеживается, .gitignore не перестает отслеживать его автоматически. Наличие реального действующего секрета в истории требует его смены/отзыва; чистка истории выполняется согласованно, а не наугад во время пары. Полный цикл разбирается на S09.

В своем продукте реальные секреты передаются нужному процессу/CI-шагу через предназначенный механизм. Не записывайте их в Dockerfile, workflow, безопасный пример или аргументы сборки. Если секретов проекту не нужно, искусственно создавать их для сдачи не требуется.

## 9. Дополнительная ручная проверка контейнера в GitHub

После публикации файлов в default branch можно открыть Actions → S08 Container check → Run workflow. Он проверяет Compose, собирает и запускает стенд на GitHub runner, проверяет HTTP, healthcheck, непривилегированного пользователя и отсутствие /app/.env; сохраняет логи и останавливает стенд. Это отдельный запуск, обычный push его не включает. Выбирайте ветку с корректным тестом и нужным кодом; файл workflow должен присутствовать в default branch.

В подготовке комплекта этот удаленный workflow не запускался. Зеленого значка в поставке нет: фактический статус появляется только после запуска в вашей копии. Этот маршрут помогает приемке, но не подменяет локальное знакомство с Docker.

## 10. Результат занятия и перенос в продукт

На занятии предъявите свою учебную копию: зеленый CI, красный диагностированный прогон и исправление, связь результата с commit, контейнер и Compose-проверку либо конкретный технический блокер и действие по его устранению. Достаточно существующих ссылок и отчетов; большой новый отчет не требуется.

До S09 перенесите в **командный продукт** workflow, Dockerfile и Compose с адаптацией под его пути, SDK, тесты и зависимости. Успешно собирается и тестируется ваш код, запускается ваше приложение. Скопированный SampleApi не считается инфраструктурой вашего продукта. Если .gitignore уже существует, дополните его, не заменяя целиком.

Рубеж этапа 4: рабочие CI и автоматические тесты, локальный образ/стенд, корректная передача реально нужной конфигурации/секретов, результат с привязкой к версии и воспроизведение по инструкции другим участником. На занятии начинается эта работа; завершение до S09 подтверждается по артефактам продукта без отдельного дополнительного контрольного элемента. Анализаторы подключаются по мере введения начиная с S09.

## 11. Диагностика

- SDK не найден — сравните установленную версию с global.json; откройте новый терминал после установки.
- Docker Client отвечает, Server нет — запустите Docker Desktop, проверьте Linux containers и его диагностическое сообщение.
- CI не появился — проверьте свою копию, наличие .github/workflows/ci.yml, разрешение Actions и выбранную ветку. Push обрабатывается для всех веток; PR может создать отдельный прогон.
- CI красный — найдите первый упавший шаг. Restore не равен Test; отсутствие TRX после ошибки восстановления ожидаемо.
- Код изменен, а тест старый — перед --no-build выполните build в Release.
- Порт занят — остановите предыдущий API/контейнер либо измените APP_PORT в .env и адрес запросов. Внутри контейнера остается 8080.
- После правки .env ответ прежний — выполните compose up повторно, проверьте docker compose config и переменные терминала.
- Непонятное состояние контейнера — docker compose ps --all и docker compose logs --tail 50; затем исправление установленной причины.

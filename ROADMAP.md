# Capitalism Roadmap

Create a fun game in the style of Capitalism II, where players experience realistic market dynamics, strategy trade-offs, and fair competitive mechanics without exploit paths.

## Active issues to work on

### Onboarding

- [x] Remove the company name selection and person name selection from the onboarding. Keep auto generated name, but hide the name selection. Users can change the name later in the game.
- [x] On mobile, when user clicks on any button in the onboarding after he scrolled little down, the screen goes to top which creates undesired confusion.
- [x] In the purchase factory step and purchase sales shop, make one of the recommended choices selected one, so that user can just click continue.
- [x] In the sales shop purchase onboarding step show on map the distance from the factory. The main point is to optimize the distance between the factory while tuning up the retail index

### Copy-Paste units

- [x] Copy-paste of the units works fine on desktop with keyboard, however this feature is not available for small devices at the moment. Add on the grid page also copy and paste buttons when building is in editation mode.

### News

- [x] Add pagination to each category of news. At each category make sure to show top 10 recent news. At the moment if there if there is more then 10 news from reporting category and i select the changelog category, it does not show any items.

### Emails

- [x] Setup email using Email Communication Services in azure
- [x] Create templates for emails using handlebars rendered from the html template file. All emails must have same design. Create professional looking email template. Make sure each localization works for every supported language.
- [x] When user never received email (create flag in the master database), send him the registration email. In the email also write his current url address which he accessed.
- [x] Send users email on weekly basis in friday noon with the report where will be listed all their active game servers, their profit and ranking in the game server, then the master server bounties points they collected in past week, and if there are any news from the changelog within a week add it there.
- [x] Store the language preference after the user logs in to the system to the master server database, and use that localization for the emails to be sent. If no language is set in the database use English.

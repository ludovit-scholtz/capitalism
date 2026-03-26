# Capitalism Roadmap

Create a fun game on style of the capitalism II game. 

It will use real world map. The game will start in single city and later other cities will be added.

## Buildings

Player can buy the buildings:
- mines, 
- factories, 
- sales shops, 
- research and development buildings,
- appartment buildings, 
- commercial buildings,
- media houses - Newspaper, Radio, TV, 
- banks, 
- exchanges
- Power plants - coal, gas, nuclear, solar, wind.

Building can be set for sale and other players can buy the building. Each building requires power.

Mines, factories, sales shops, and r&d buildings will have configuration option with 4x4 units grid. Grid units can be matched with units next to each other - link will be active or inactive. Diagonal links can be also active or inactive or active in both diagonals.

Mines unit grid allows:
- Mining operation unit
- Storage unit
- B2B sales unit

Factories unit grid allows:
- Purchase unit
- Manufacturing unit
- Branding unit
- Storage unit
- B2B sales unit

Sales shops unit grid allows:
- Purchase unit
- Marketing unit
- Public sales unit

Research and development building allows units:
- Product quality
- Marketing brand quality

Appartment buildings and commercial buildings allows to set the price per m^2. After the change the price is applied after 1 day. The appartment building has occupancy and fixed size. If price is higher then average in the area, the occupancy percentage goes down and vice versa. It is more difficult to reach full occupancy.

Media houses improve brand quality.

Banks allows to borrow to player money. Player can configure the interest rate in the bank.

## Ranking

Each player is ranked by his total wealth. Players can start multiple companies. Company pays out the dividends.

## Units configuration

Mining operation unit - produces raw materials. Depending on the resource type on the mine, it can produce different raw materials such as coal, iron, gold, chemical minerals, wood etc. The production rate can be increased by upgrading the unit.

Storage unit - allows to store raw materials or finished products. The storage capacity can be increased by upgrading the unit.

B2B sales unit - allows to sell raw materials at the place, or ship it to the exchange warehouse. Sell onsite can be public, limitted to the company or limitted to users companies. Storage size at the sales can be increased by upgrading the unit. User can set the minimum price to be received.

Purchase unit - allows to purchase products from the exchange warehouse or from other players. The purchase capacity can be increased by upgrading the unit. The maximum purchase price can be set by the player. The purchase can be locked for specific vendor, specific exchange or can be set to buy at the optimal price. The minimum product quality can be set by the player. The purchase unit can be set to buy raw materials or finished products.

Manufacturing unit - allows to manufacture products from raw materials linked to the manufacturing unit. The manufacturing speed and storage size can be increased by upgrading the unit. The player can set the product type to be manufactured. The quality of manufactured product depends on the quality of raw materials and the quality of the researched product. The quality can be increased by upgrading the unit.

Branding unit - allows to set the brand of the products manufactured in the factory. The brand can be product specific, product category specific or company specific. This unit is not upgradable. Brand quality affects the sales of the products. Higher brand awareness and brand quality means more sales.

Marketing unit - allows to set budget for the linked products. The money is paid to the selected media house. Marketing unit increases the product's brand awareness.

Public sales unit - allows to sell products directly to general public. The sales capacity can be increased by upgrading the unit. The player can set the minimum price for the products sold in this unit. The sales can be limited to specific company or open to all players.

## Timing

Each change - new building, change of the building unit plan, or upgrade of the unit takes specific number of ticks to be executed.

## The onboarding 

New players when comes to the web first select the industry type they want to start with. The Furniture, Food processing, or Healthcare.

Then they pick the location of their first factory and select the first product which they want to produce. This will set the factory layout for them. Wizzard will show them important areas on the screen like how much money they have, the price configuration or public sales configuration.

Next the player buys his first sales shop and configures it to set the sales price to public.

The player is shown that the time goes on and he makes the profit from his business.

## Game engine

Game is played in ticks. In each tick the game state for all buildings is recalculated, the products are sold, the free storage capacity is used to to fill in from the linked units, r&d is recalculated, brading is recalculated, occupancy and rents are calculated. The game engine runs in backend.

## Taxes

At specific tick rounds the taxes are calculated.

## Encyclopedy 

All combination of products are visible in the manufacturing encyclopedy which serves as in game documentation.

## Chat

In game chat will be possible if user links his account with the discord.

## Monetization

Startup pack will be available after user finish with the onboarding. There will be time limited time offer.

Startup pack will include - 3 months of pro subscription and in game currency.  

In pro subscription the players will have more products to manufacture and sell.

# Technical implementation

Frontend is vue.js with source code located at projects/frontend.

Backend is .NET with graphql engine with data stored in postgresql.

Deployed to kubernetes.

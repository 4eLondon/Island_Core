create table if not exists users (
    username        text primary key,
    email           text unique not null,
    first_name      text not null,
    last_name       text not null,
    telephone       text,
    city            text,
    gender          text,
    password_hash   text not null,
    created_utc     timestamptz default now()
);

create table if not exists inventory (
    sku              text primary key,
    product_name     text not null,
    category         text,
    stock            integer not null default 0,
    reorder_level    integer not null default 0,
    supplier         text,
    status           text,
    cost_per_unit    integer not null default 0,
    total_order_cost integer not null default 0,
    created_utc      timestamptz default now()
);

create table if not exists sales (
    id              bigint generated always as identity primary key,
    created_utc     timestamptz default now(),
    customer        text,
    sku             text references inventory(sku) on delete set null,
    product_name    text,
    quantity        integer not null default 0,
    unit_price      integer not null default 0,
    total_amount    integer not null default 0
);

create index if not exists idx_inventory_category on inventory(category);
create index if not exists idx_inventory_supplier on inventory(supplier);
create index if not exists idx_sales_created     on sales(created_utc);
create index if not exists idx_sales_customer    on sales(customer);
create index if not exists idx_sales_sku         on sales(sku);

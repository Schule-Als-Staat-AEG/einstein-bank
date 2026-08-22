<script lang="ts">
  export let title: string;
  export let description: string;
</script>

<div class="flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
  <div>
    <h1 class="text-2xl font-bold tracking-tight sm:text-3xl">{title}</h1>
    <p class="mt-1 text-sm text-base-content/65">{description}</p>
  </div>
  <div class="mt-3 flex gap-2 sm:mt-0">
    <slot />
  </div>
</div>
